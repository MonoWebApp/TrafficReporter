﻿using TrafficReporter.DAL;
using TrafficReporter.Model;
using TrafficReporter.Model.Common;
using TrafficReporter.Repository.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using TrafficReporter.Common;
using TrafficReporter.Common.Enums;
using TrafficReporter.Common.Filter;
using TrafficReporter.DAL.Entity_Models;

namespace TrafficReporter.Repository
{
    /// <summary>
    /// This class contains implemented methods for accesing database and
    /// querying trough it.
    /// </summary>
    public class ReportRepository : IReportRepository
    {
        private readonly string _connectionString;

        #region Constructors


        /// <summary>
        /// Connection string is now injected trough ninject module.
        /// <see cref="DIModule"/>
        /// </summary>
        /// <param name="connectionString">To be injected</param>
        public ReportRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Adds the report to database.
        /// </summary>
        /// <param name="report">The report to be added to database.</param>
        /// <returns>
        /// Number of rows affected by removing(should be 1).
        /// </returns>
        public async Task<int> AddReportAsync(IReport report)
        {
            var rowsAffrected = 0;


            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                //This method should add only if other report in range and with same
                //cause doesn't exist.
                //todo move this logic upwards
                var reportInRangeId = await CheckIfOtherReportInRangeAsync(report, connection);
                if (!reportInRangeId.Equals(Guid.Empty))
                {
                    int rowsAffected = await UpdateTimeAndRatingAsync(reportInRangeId, report.Cause, connection);
                    connection.Close();
                    return (int) Inserted.Updated;
                }

                #region AddReport

                using (var command = new NpgsqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText =
                        "INSERT INTO trafreport (id, cause, direction, longitude, lattitude, date_created)" +
                        "VALUES (@id, @cause, @direction, @longitude, @lattitude, @date_created)";
                    command.Parameters.AddWithValue("id", report.Id);
                    command.Parameters.AddWithValue("cause", report.Cause);
                    command.Parameters.AddWithValue("direction", (int) report.Direction);
                    command.Parameters.AddWithValue("longitude", report.Longitude);
                    command.Parameters.AddWithValue("lattitude", report.Lattitude);
                    //todo do not use datetime.now here, get time from frontend
                    command.Parameters.AddWithValue("date_created", report.DateCreated);
                    rowsAffrected = await command.ExecuteNonQueryAsync();
                }
                await UpdateTimeAndRatingAsync(report.Id, report.Cause, connection);

                #endregion AddReport

                connection.Close();
            }

            return (int) Inserted.Added;
        }

        /// <summary>
        /// Updates report which has passed id by reseting time_remaining and incrementing rating.
        /// </summary>
        /// <param name="reportInRangeId">Unique identifier.</param>
        /// <param name="cause">Cause used to get amount for time reseting.</param>
        /// <param name="connection">Connection to PostgreSql.</param>
        private async Task<int> UpdateTimeAndRatingAsync(Guid reportInRangeId, int cause, NpgsqlConnection connection)
        {
            int rowsAffected = 0;

            using (var command = new NpgsqlCommand(
                $"UPDATE trafreport SET time_remaining = (SELECT time_remaining FROM cause_table WHERE id = {cause}), rating = rating + 1 " +
                $"WHERE id = '{reportInRangeId}'", connection))
            {
                rowsAffected = await command.ExecuteNonQueryAsync();
            }

            return rowsAffected;
        }

        ///  <summary>
        ///  This is a private method for the add method which checks
        ///  if other report of same cause and within range exists.
        ///  </summary>
        ///  <param name="report">Report for which we want to check if there are other reports in range of this one.</param>
        /// <param name="connection">Connection to PostgreSQL</param>
        /// <returns>Report id within range if it exists.(We need only one id for updating(reset time and increase rating).)</returns>
        /// todo should we check for same id?
        private async Task<Guid> CheckIfOtherReportInRangeAsync(IReport report, NpgsqlConnection connection)
        {
            Guid returnValue = Guid.Empty;

            using (var command =
                new NpgsqlCommand(
                    $"SELECT id\r\n" +
                    $"FROM (SELECT id FROM trafreport\r\n" +
                    $" WHERE cause = {report.Cause}" +
                    $" AND calculate_distance({report.Longitude}, {report.Lattitude}, longitude, lattitude)  < (SELECT cause_range FROM cause_table WHERE id = {report.Cause})) AS id\r\n",
                    connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            returnValue = reader.GetDataSafely<Guid>("id");
                        }
                    }

                    reader.Close();
                }
            }

            return returnValue;
        }

        public async Task<IReport> GetReportAsync(Guid id)
        {
            IReport report = null;


            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand($"SELECT * FROM trafreport WHERE id = '{id}'", connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {

                                report = new Report();
                                report.Id = id;
                                report.Cause = reader.GetDataSafely<int>("cause");
                                report.Rating = reader.GetDataSafely<int>("rating");
                                report.Direction = reader.GetDataSafely<Direction>("direction");
                                report.Longitude = reader.GetDataSafely<double>("longitude");
                                report.Lattitude = reader.GetDataSafely<double>("lattitude");
                                report.DateCreated = reader.GetDataSafely<DateTime>("date_created");
                           
                        }
                    }
                    reader.Close();
                }
                connection.Close();
            }

            return report;
        }

        /// <summary>
        /// Gets all reports from database which satisfy passed filter.
        /// </summary>
        /// <param name="filter"></param>
        /// <returns>
        /// Collection of reports that satisfy filters.
        /// </returns>
        public async Task<IEnumerable<IReport>> GetFilteredReportsAsync(IFilter filter)
        {
            List<IReport> reports = new List<IReport>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand())
                {
                    command.Connection = connection;
                    var commandText = new StringBuilder("SELECT * FROM trafreport ");

                    //If there is at least one filter, then apply
                    //WHERE part of the SQL query.
                    if (filter != null)
                    {
                        commandText.Append("WHERE ");

                        //This is filtering like here:
                        //https://timdams.com/2011/02/14/using-enum-flags-to-write-filters-in-linq/
                        if (filter.Cause != 0)
                        {
                            //I'm here adding AND  because coordinates must be specified or
                            //db could be outputting reports that might be out of the area
                            //of visible map.
                            commandText.Append($"cause & {filter.Cause} > 0 AND ");
                        }


                        commandText.Append($"longitude BETWEEN {filter.LowerLeftX} AND {filter.UpperRightX} AND ");
                        commandText.Append($"lattitude BETWEEN {filter.LowerLeftY} AND {filter.UpperRightY}");

                        commandText.Append($"");

                        commandText.Append($"LIMIT {filter.PageSize}");
                    }
                    command.CommandText = commandText.ToString().Replace(',', '.');

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            var report = new Report();
                            report.Id = reader.GetDataSafely<Guid>("id");
                            report.Cause = reader.GetDataSafely<int>("cause");
                            report.Rating = reader.GetDataSafely<int>("rating");
                            report.Direction = reader.GetDataSafely<Direction>("direction");
                            report.Longitude = reader.GetDataSafely<double>("longitude");
                            report.Lattitude = reader.GetDataSafely<double>("lattitude");
                            report.DateCreated = reader.GetDataSafely<DateTime>("date_created");
                            reports.Add(report);
                        }

                        reader.Close();
                    }
                }
                connection.Close();
            }

            return reports;
        }

        /// <summary>
        /// Removes the report from database by passing Id parameter.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        /// Number of rows affected by removing(should be 1).
        /// </returns>
        //todo this method might be removed because we don't want anyone else but db to delete reports
        public async Task<int> RemoveReportAsync(Guid id)
        {
            var rowsAffrected = 0;

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText =
                        "DELETE FROM trafreport " +
                        $"WHERE id='{id}'";
                    rowsAffrected = await command.ExecuteNonQueryAsync();
                }
            }

            return rowsAffrected;
        }

        #endregion Methods
    }
}