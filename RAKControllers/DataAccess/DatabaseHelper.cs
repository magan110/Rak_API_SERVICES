using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace RAKControllers.DataAccess
{
    public class DatabaseHelper
    {
        private readonly string _bwliveConnectionString;

        public DatabaseHelper(string bwliveConnectionString)
        {
            _bwliveConnectionString = bwliveConnectionString;
        }

        // General method to execute SELECT queries for any connection string
        private List<Dictionary<string, object>> ExecuteSelectQuery(string connectionString, string query, Dictionary<string, object> parameters)
        {
            var resultList = new List<Dictionary<string, object>>();

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader[i];
                        }
                        resultList.Add(row);
                    }
                }
            }

            return resultList;
        }

        // General method to execute INSERT, UPDATE, or DELETE queries for any connection string
        private int ExecuteCommand(string connectionString, string query, Dictionary<string, object> parameters)
        {
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                conn.Open();
                return cmd.ExecuteNonQuery(); // Returns the number of rows affected
            }
        }

        private int ExecuteCommandInternal(string connectionString, string query, Dictionary<string, object> parameters)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(query, conn);

                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        public int WebExecute(string query, Dictionary<string, object> parameters)
        {
            return ExecuteCommandInternal(_bwliveConnectionString, query, parameters);
        }


        // Function to handle queries for bwlive database (WebSessBean)
        public List<Dictionary<string, object>> WebSessBean(string query, Dictionary<string, object> parameters)
        {
            return ExecuteSelectQuery(_bwliveConnectionString, query, parameters);
        }

        // Retrieve SecretKey for a given PartnerID from the RegisteredUsers table (itKharia)
        public string GetSecretKey(string userID)
        {
            var query = "SELECT appRegId FROM wcmUserCred WHERE loginIdM = @loginIdm and isActive = 'Y'";
            var parameters = new Dictionary<string, object>
            {
                { "@loginIdm", userID } // ✅ Matches query
            };

            var result = WebSessBean(query, parameters);
            if (result.Count > 0)
            {
                return result[0]["appRegId"]?.ToString();
            }

            throw new Exception("appRegId not found for the given UserID.");
        }
    }
}
