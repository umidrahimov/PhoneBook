using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Phonebook_Backend.Server;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace AbbTech
{
    class Helper
    {
        protected HttpServer server;
        public IConfigurationRoot Configuration { get; set; }
        string connection;
        //IConfigManager config;
        //IDAL dal;

        public Helper()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json").Build();

            string endpointUrl = Configuration.GetSection("listener").GetSection("endpointUrl").Value;
            server = new HttpServer(endpointUrl);
            connection = Configuration["ConnectionStrings:Default"];
        }

        public void Start()
        {
            server.MessageReceived += Server_MessageReceived;
            server.Open();
        }

        public void Stop()
        {
            server.Close();
        }

        private void Server_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            HttpRequest request = (HttpRequest)e.Request;
            //TODO: Write with regex instead of String.Split
            //string pattern = @"(?<=user\/)[^.\s]*(?=\/)*";
            //Match m = Regex.Match(request.URI, pattern);
            //string command = m.Value;

            string command = request.URI.Split('/')[2];

            HttpResponse response = new HttpResponse();

            switch (command)
            {
                case ("get"):
                    response = ProcessGetRequest(request);
                    break;
                case ("add"):
                    response = ProcessAddRequest(request);
                    break;
                case ("edit"):
                    response = ProcessEditRequest(request);
                    break;
                case ("list"):
                    response = ProcessListRequest(request);
                    break;
                case ("delete"):
                    response = ProcessDeleteRequest(request);
                    break;
                case ("status"):
                    response = ProcessStatusRequest(request);
                    break;
                default:
                    response.Body = "Wrong URI";
                    response.StatusCode = 500;
                    break;
            }
            e.Response = response;
        }
        private HttpResponse ProcessAddRequest(HttpRequest request)
        {
            HttpResponse response = new HttpResponse();

            User user = JsonConvert.DeserializeObject<User>(request.Body);

            using (MySqlConnection conn = new MySqlConnection(connection))
            {
                conn.Open();
                string query = $"INSERT INTO users (name, phone) VALUES (\"{user.name}\", \"{user.phone}\");";
                query += "SELECT LAST_INSERT_ID();";
                MySqlCommand cmd = new MySqlCommand(query, conn);
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();

                    AddResponse addResponse = new AddResponse()
                    {
                        user_id = reader.GetInt32(0),
                        operation_type = Operation_type.add,
                        operation_status = (reader.RecordsAffected > 0) ? Operation_status.success : Operation_status.fail
                    };
                    response.StatusCode = 200;
                    response.Body = JsonConvert.SerializeObject(addResponse, new Newtonsoft.Json.Converters.StringEnumConverter());
                }
            }

            return response;

        }

        private HttpResponse ProcessEditRequest(HttpRequest request)
        {
            HttpResponse response = new HttpResponse();

            User user = JsonConvert.DeserializeObject<User>(request.Body);

            using (MySqlConnection conn = new MySqlConnection(connection))
            {
                conn.Open();

                string query = $"UPDATE users SET name = \"{user.name}\", phone =\"{user.phone}\" \n";
                query += $"WHERE user_id={user.user_id}";

                MySqlCommand cmd = new MySqlCommand(query, conn);

                int queryResult = cmd.ExecuteNonQuery();

                EditResponse editResponse = new EditResponse()
                {
                    user_id = user.user_id,
                    operation_type = Operation_type.edit,
                    operation_status = (queryResult == 1) ? Operation_status.success : Operation_status.fail
                };

                response.StatusCode = 200;
                response.Body = JsonConvert.SerializeObject(editResponse, new Newtonsoft.Json.Converters.StringEnumConverter());
            }

            return response;
        }

        private HttpResponse ProcessDeleteRequest(HttpRequest request)
        {
            HttpResponse response = new HttpResponse();

            using (MySqlConnection conn = new MySqlConnection(connection))
            {
                conn.Open();

                string query = $"DELETE from users WHERE user_id={request.Parameters["user_id"]}\n";

                MySqlCommand cmd = new MySqlCommand(query, conn);

                int queryResult = cmd.ExecuteNonQuery();

                DeleteResponse editResponse = new DeleteResponse()
                {
                    user_id = Int32.Parse(request.Parameters["user_id"]),
                    operation_type = Operation_type.delete,
                    operation_status = (queryResult == 1) ? Operation_status.success : Operation_status.fail
                };

                response.StatusCode = 200;
                response.Body = JsonConvert.SerializeObject(editResponse, new Newtonsoft.Json.Converters.StringEnumConverter());
            }

            return response;
        }

        private HttpResponse ProcessStatusRequest(HttpRequest request)
        {
            HttpResponse response = new HttpResponse();
            response.StatusCode = 200;

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connection))
                {
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand("SELECT 1", conn);

                    int queryResult = cmd.ExecuteNonQuery();

                    if (queryResult == 1)
                    {
                        response.Body = "{\"status\": \"OK\"}";
                    }
                }

            }
            catch (Exception)
            {
                response.Body = "{\"status\": \"Failed\"}";
                return response;
            }

            return response;
        }
        private HttpResponse ProcessListRequest(HttpRequest request)
        {
            HttpResponse response = new HttpResponse();

            using (MySqlConnection conn = new MySqlConnection(connection))
            {
                conn.Open();

                string query = $"SELECT user_id, name, phone FROM users;";

                MySqlCommand cmd = new MySqlCommand(query, conn);
                List<User> list = new List<User>();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        User user = new User()
                        {
                            user_id = reader.GetInt32(0),
                            name = reader.GetString(1),
                            phone = reader.GetString(2)
                        };
                        list.Add(user);
                    }
                }

                response.StatusCode = 200;
                response.Body = JsonConvert.SerializeObject(list, new Newtonsoft.Json.Converters.StringEnumConverter());
            }

            return response;

        }
        private HttpResponse ProcessGetRequest(HttpRequest request)
        {
            HttpResponse response = new HttpResponse();

            using (MySqlConnection conn = new MySqlConnection(connection))
            {
                conn.Open();
                string query = $"SELECT user_id, name, phone FROM users \n";
                query += $"WHERE user_id={request.Parameters["id"]}";
                MySqlCommand cmd = new MySqlCommand(query, conn);
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();

                    User user = new User()
                    {
                        user_id = reader.GetInt32(0),
                        name = reader.GetString(1),
                        phone = reader.GetString(2)
                    };

                    response.StatusCode = 200;
                    response.Body = JsonConvert.SerializeObject(user, new Newtonsoft.Json.Converters.StringEnumConverter());
                }
            }

            return response;

        }

    }


}
