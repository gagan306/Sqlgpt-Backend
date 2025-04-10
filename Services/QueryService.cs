using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
            // Log the incoming question
            _logger.LogInformation("Processing question: {Question}", question);

            // Generate SQL query from question
            string sqlQuery = await GenerateSQLQueryFromQuestion(question);
            if (string.IsNullOrWhiteSpace(sqlQuery))
            {
                _logger.LogError("Failed to generate SQL query from question: {Question}", question);
                throw new Exception("Failed to generate SQL query from the question.");
            }

            _logger.LogInformation("Generated SQL query: {SqlQuery}", sqlQuery);

            // Execute the SQL query
            object queryResult = await ExecuteSQLQueryAsync(sqlQuery);

            // Generate structured answer from query result
            string structuredAnswer = await GenerateStructuredAnswer(question, sqlQuery, queryResult);

            return (sqlQuery, queryResult, structuredAnswer);
        }

        private async Task<string> GenerateSQLQueryFromQuestion(string question)
        {
            try
            {
                string prompt = $"Convert the following question into a valid MSSQL query:\n\nQuestion: {question}\nSQL Query:";

                // Using explicit class instead of anonymous type
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
                client.Timeout = TimeSpan.FromSeconds(30); // Set a reasonable timeout

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                _logger.LogInformation("Sending request to OpenAI API");

                HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI API error: Status {StatusCode}, Response: {Response}",
                        response.StatusCode, responseContent);
                    throw new Exception($"OpenAI API returned error: {response.StatusCode}");
                }

                _logger.LogDebug("OpenAI API response: {Response}", responseContent);

                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    _logger.LogError("Empty response from OpenAI API");
                    throw new Exception("Empty response from OpenAI API");
                }

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
                // Re-throw API-specific exceptions without wrapping
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
                    CommandType = CommandType.Text,
                    CommandTimeout = 30 // Set a reasonable timeout
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
                throw; // Let the caller handle SQL exceptions
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

                // Using explicit class instead of anonymous type
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
                client.Timeout = TimeSpan.FromSeconds(30); // Set a reasonable timeout

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                _logger.LogInformation("Sending request to OpenAI API for answer generation");

                HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI API error during answer generation: Status {StatusCode}, Response: {Response}",
                        response.StatusCode, responseContent);
                    return "Failed to generate structured answer due to API error.";
                }

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