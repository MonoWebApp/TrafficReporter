﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using TrafficReporter.Model.Common;
using TrafficReporter.Service.Common;
using System.Threading.Tasks;
using Ninject;
using AutoMapper;
using TrafficReporter.Common.Enums;


//using System.Web.Mvc;
using System.Web.Http;
using System.Web.Mvc;
using TrafficReporter.Common.Filter;
using TrafficReporter.WebAPI.ViewModels;


namespace TrafficReporter.WebAPI.Controllers
{
    [System.Web.Http.RoutePrefix("api/report")]
    public class ReportController : ApiController
    {
        private readonly IReportService _reportService;
        private readonly IMapper _mapper;
        private readonly IFilterFactory _filterFactory;


        public ReportController(IReportService reportService, IMapper mapper, IFilterFactory filterFactory)
        {
            _reportService = reportService;
            _mapper = mapper;
            _filterFactory = filterFactory;
        }


        /// <summary>
        /// Adds the report asynchronous.
        /// </summary>
        /// <param name="report">The report.</param>
        /// <returns>Success of this operation</returns>
        [System.Web.Http.HttpPost]
        [RequireHttps]
        public async Task<Inserted> AddReportAsync(ReportViewModel report)
        {
            return await _reportService.AddReportAsync(_mapper.Map<IReport>(report));
        }


        /// <summary>
        /// Removes the report asynchronous.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>Returns number of reports removed.</returns>
        ///todo remove this method because we dont want users to remove reports
        [System.Web.Http.HttpDelete]
        [System.Web.Http.Route("{id:guid}")]
        [RequireHttps]
        public async Task<int> RemoveReportAsync(Guid id)
        {
            return await this._reportService.RemoveReportAsync(id);
        }


        /// <summary>
        /// Getreports the asynchronous.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>Report with given identifier.</returns>
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("{id:guid}")]
        [RequireHttps]
        public async Task<IReport> GetReportAsync(Guid id)
        {
            return await this._reportService.GetReportAsync(id);
        }


        /// <summary>
        /// Gets the filtered reports asynchronously.
        /// </summary>
        /// <param name="dx">The x coordinate of lower point.</param>
        /// <param name="dy">The y coordinate of lower point.</param>
        /// <param name="ux">The x coordinate of upper point.</param>
        /// <param name="uy">The y coordinate of upper point.</param>
        /// <param name="cause">
        /// The cause is sent as a single number by which we search
        /// using flag enums. See here: https://timdams.com/2011/02/14/using-enum-flags-to-write-filters-in-linq/
        /// </param>
        /// <param name="pageNumber">The page number.</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <returns>Reports that pass the filter</returns>
        [System.Web.Http.HttpGet]
        [RequireHttps]
        public async Task<IEnumerable<IReport>> GetFilteredReportsAsync(double dx, double dy, double ux, double uy,
            int cause, int pageNumber = 1, int pageSize = 10)
        {

            IFilter filter = _filterFactory.GetFilter(dx, dy, ux, uy, cause, pageNumber, pageSize);


            var result = await _reportService.GetFilteredReportsAsync(filter);

            if (result != null)
            {
                return result;
            }
            else
            {
                return new List<IReport>();
            }
        }
    }
}