// <copyright file="CarRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace perftest.Baseline
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using dddlib.Persistence;

    public class CarRepository
    {
        private readonly string connectionString;

        public CarRepository(string connectionString)
        {
            this.connectionString = connectionString;

            new SqlConnection(this.connectionString).InitializeSchema("baseline", typeof(CarRepository));
        }

        public Car Load(string registration)
        {
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "baseline.LoadCar";
                command.Parameters.Add("@Registration", SqlDbType.VarChar, 10).Value = registration;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        return new Car
                        {
                            Registration = Convert.ToString(reader["Registration"]),
                            TotalDistanceDriven = Convert.ToInt32(reader["TotalDistanceDriven"]),
                        };
                    }
                }
            }

            throw new AggregateRootNotFoundException();
        }

        public void Save(Car car)
        {
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "baseline.SaveCar";
                command.Parameters.Add("@Registration", SqlDbType.VarChar, 10).Value = car.Registration;
                command.Parameters.Add("@TotalDistanceDriven", SqlDbType.Int).Value = car.TotalDistanceDriven;
                command.Parameters.Add("@IsDestroyed", SqlDbType.Bit).Value = car.IsDestroyed;

                connection.Open();

                command.ExecuteNonQuery();
            }
        }
    }
}
