using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ChatApi.Services
{
    public class QueryService
    {
        private readonly string _openAiApiKey;
        private readonly string _connectionString;
        private readonly IHttpClientFactory _clientFactory;

        public QueryService(IConfiguration configuration, IHttpClientFactory clientFactory)
        {
            _openAiApiKey = configuration["OpenAI:ApiKey"];
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _clientFactory = clientFactory;
        }

        public async Task<(string SQLQuery, object QueryResult, string StructuredAnswer)> ProcessQuestionAsync(string question)
        {
            string sqlQuery = await GenerateSQLQueryFromQuestion(question);
            if (string.IsNullOrWhiteSpace(sqlQuery))
            {
                throw new Exception("Failed to generate SQL query from the question.");
            }

            object queryResult = await ExecuteSQLQueryAsync(sqlQuery);
            string structuredAnswer = await GenerateStructuredAnswer(question, sqlQuery, queryResult);

            return (sqlQuery, queryResult, structuredAnswer);
        }

        private async Task<string> GenerateSQLQueryFromQuestion(string question)
        {
            string prompt = $"Convert the following question into a valid MSSQL query:\n\nQuestion: {question}\nSQL Query:";
            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new object[]
                {
                    new { role = "system", content = "You are a SQL generator for an MSSQL database." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.0
            };

            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonResponse);
            string sqlQuery = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return sqlQuery?.Trim();
        }

        private async Task<object> ExecuteSQLQueryAsync(string sqlQuery)
        {
            DataTable table = new DataTable();

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(sqlQuery, connection)
            {
                CommandType = CommandType.Text
            };

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();
            table.Load(reader);

            return table;
        }

        private async Task<string> GenerateStructuredAnswer(string question, string sqlQuery, object queryResult)
        {
            string resultsJson = JsonSerializer.Serialize(queryResult);
            string prompt = $"Given the question: \"{question}\", SQL: \"{sqlQuery}\", and result in JSON: {resultsJson}, provide a clear, structured answer summarizing the result.";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new object[]
                {
                    new { role = "system", content = "You are a data analyst summarizing SQL results into a readable answer." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.5
            };

            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            if (!response.IsSuccessStatusCode)
            {
                return "Failed to generate structured answer.";
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonResponse);
            string answer = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return answer?.Trim();
        }
    }
}
