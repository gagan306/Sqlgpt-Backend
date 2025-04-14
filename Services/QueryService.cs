using System;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace ChatApi.Services
{
    public class QueryService
    {
        private readonly string _openAiApiKey;
        private readonly string _connectionString;
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<QueryService> _logger;
        private const int MaxRetries = 5;  // Increased maximum retries

        public QueryService(IConfiguration configuration, IHttpClientFactory clientFactory, ILogger<QueryService> logger)
        {
            _openAiApiKey = configuration["OpenAI:ApiKey"] ?? throw new ArgumentNullException("OpenAI API key is missing in configuration");
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new ArgumentNullException("Database connection string is missing in configuration");
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(string SQLQuery, object QueryResult, string StructuredAnswer)> ProcessQuestionAsync(string question)
        {
            _logger.LogInformation("Processing question: {Question}", question);

            string sqlQuery = await GenerateSQLQueryFromQuestion(question);
            if (string.IsNullOrWhiteSpace(sqlQuery))
            {
                _logger.LogError("Failed to generate SQL query from question: {Question}", question);
                throw new Exception("Failed to generate SQL query from the question.");
            }

            _logger.LogInformation("Generated SQL query: {SqlQuery}", sqlQuery);

            object queryResult = await ExecuteSQLQueryAsync(sqlQuery);
            string structuredAnswer = await GenerateStructuredAnswer(question, sqlQuery, queryResult);

            return (sqlQuery, queryResult, structuredAnswer);
        }

        private async Task<string> GenerateSQLQueryFromQuestion(string question)
        {
            try
            {
                string prompt = $"Convert the following question into a valid MSSQL query:\n\nQuestion: {question}\nSQL Query:";
                var systemMessage = new Message { Role = "system", Content = "You are a SQL generator for an MSSQL database." };
                var userMessage = new Message { Role = "user", Content = prompt };

                var requestBody = new OpenAiRequest
                {
                    Model = "gpt-3.5-turbo",
                    Messages = new List<Message> { systemMessage, userMessage },
                    Temperature = 0.0f
                };

                using var client = _clientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
                client.Timeout = TimeSpan.FromSeconds(30);

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                _logger.LogInformation("Sending request to OpenAI API for SQL generation");
                string responseContent = await PostWithRetriesAsync(client, content, "https://api.openai.com/v1/chat/completions", "SQL query generation");

                _logger.LogDebug("OpenAI API response: {Response}", responseContent);

                using JsonDocument document = JsonDocument.Parse(responseContent);
                if (document.RootElement.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
                {
                    JsonElement messageElement = choices[0].GetProperty("message");
                    string? sqlQuery = messageElement.GetProperty("content").GetString();

                    if (string.IsNullOrWhiteSpace(sqlQuery))
                    {
                        _logger.LogError("Empty SQL query returned from OpenAI API");
                        throw new Exception("Empty SQL query returned from OpenAI API");
                    }

                    return sqlQuery.Trim();
                }
                else
                {
                    _logger.LogError("Unable to extract SQL query from OpenAI API response");
                    throw new Exception("Unable to extract SQL query from OpenAI API response");
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error while connecting to OpenAI API");
                throw new Exception("Failed to connect to OpenAI API", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error with OpenAI API response");
                throw new Exception("Failed to parse OpenAI API response", ex);
            }
            catch (Exception ex) when (ex.Message.Contains("API"))
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during SQL query generation");
                throw new Exception("Unexpected error during SQL query generation", ex);
            }
        }

        private async Task<object> ExecuteSQLQueryAsync(string sqlQuery)
        {
            try
            {
                DataTable table = new DataTable();

                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand(sqlQuery, connection)
                {
                    CommandType = System.Data.CommandType.Text,
                    CommandTimeout = 30
                };

                _logger.LogInformation("Executing SQL query against database");
                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                table.Load(reader);

                _logger.LogInformation("SQL query executed successfully. Rows returned: {RowCount}", table.Rows.Count);
                return table;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error executing query: {SqlQuery}", sqlQuery);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error executing SQL query: {SqlQuery}", sqlQuery);
                throw new Exception($"Error executing SQL query: {ex.Message}", ex);
            }
        }

        private async Task<string> GenerateStructuredAnswer(string question, string sqlQuery, object queryResult)
        {
            try
            {
                string resultsJson = JsonSerializer.Serialize(queryResult);
                string prompt = $"Given the question: \"{question}\", SQL: \"{sqlQuery}\", and result in JSON: {resultsJson}, provide a clear, structured answer summarizing the result.";
                var systemMessage = new Message { Role = "system", Content = "You are a data analyst summarizing SQL results into a readable answer." };
                var userMessage = new Message { Role = "user", Content = prompt };

                var requestBody = new OpenAiRequest
                {
                    Model = "gpt-3.5-turbo",
                    Messages = new List<Message> { systemMessage, userMessage },
                    Temperature = 0.5f
                };

                using var client = _clientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
                client.Timeout = TimeSpan.FromSeconds(30);

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                _logger.LogInformation("Sending request to OpenAI API for answer generation");
                string responseContent = await PostWithRetriesAsync(client, content, "https://api.openai.com/v1/chat/completions", "structured answer generation");

                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    _logger.LogError("Empty response from OpenAI API during answer generation");
                    return "Failed to generate structured answer due to empty API response.";
                }

                using JsonDocument document = JsonDocument.Parse(responseContent);
                string? answer = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(answer))
                {
                    _logger.LogError("Empty answer content from OpenAI API");
                    return "No answer generated.";
                }

                _logger.LogInformation("Successfully generated structured answer");
                return answer.Trim();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error while generating structured answer");
                return "Failed to generate structured answer due to connection issues.";
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error while generating structured answer");
                return "Failed to generate structured answer due to response parsing error.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during answer generation");
                return $"Failed to generate structured answer: {ex.Message}";
            }
        }

        /// <summary>
        /// Sends an HTTP POST request with retry logic for handling rate limit errors.
        /// Honors the Retry-After header if present.
        /// </summary>
        private async Task<string> PostWithRetriesAsync(HttpClient client, StringContent content, string url, string contextLog)
        {
            int attempt = 0;
            TimeSpan delay = TimeSpan.FromSeconds(2);

            while (attempt < MaxRetries)
            {
                attempt++;
                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return responseContent;
                }
                else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // Check if the Retry-After header is provided
                    if (response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values))
                    {
                        if (int.TryParse(System.Linq.Enumerable.FirstOrDefault(values), out int seconds))
                        {
                            delay = TimeSpan.FromSeconds(seconds);
                        }
                    }

                    _logger.LogWarning("Received TooManyRequests during {Context}. Attempt {Attempt} of {MaxRetries}. Retrying in {Delay} seconds.",
                        contextLog, attempt, MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay);
                    delay = delay * 2;  // Exponential backoff for subsequent attempts
                    continue;
                }
                else
                {
                    _logger.LogError("OpenAI API error during {Context}: Status {StatusCode}, Response: {Response}",
                        contextLog, response.StatusCode, responseContent);
                    throw new Exception($"OpenAI API returned error: {response.StatusCode}");
                }
            }

            throw new Exception("Exceeded maximum retry attempts for OpenAI API call.");
        }
    }

    // Explicit classes to replace anonymous types
    public class Message
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class OpenAiRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<Message> Messages { get; set; } = new List<Message>();
        public float Temperature { get; set; }
    }
}
