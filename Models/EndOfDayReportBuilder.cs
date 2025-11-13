using Microsoft.IdentityModel.Tokens;
using OpenOrderSystem.Core.Areas.Staff.Models;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;
using System.Text.Json;

namespace OpenOrderSystem.Core.Models
{
    public class EndOfDayReportBuilder
    {
        private EndOfDayReport _report = new EndOfDayReport();
        private TimeZoneInfo _timezone = TimeZoneInfo.Local;

        public static EndOfDayReportBuilder Create(DateTime? reportDate = null)
        {
            var bob = new EndOfDayReportBuilder();

            if (reportDate != null)
            {
                bob.SetDate(reportDate ?? DateTime.Now);
            }

            return bob;
        }

        public EndOfDayReportBuilder AddOrders(List<Order> orders)
        {
            _report.SalesReport = new Dictionary<string, SalesData>();
            _report.PromoReport = new SalesData();
            _report.OrderCount = orders.Count;
            _report.GrossSales = 0.0f;

            foreach (var order in orders)
            {
                var fixedOrder = JsonSerializer.Deserialize<LockedOrder>(order.Locked);
                _report.GrossSales += fixedOrder?.LineTotal ?? 0.0f;

                foreach (var lineItem in fixedOrder?.LineItems.Keys ?? new Dictionary<string, float>().Keys)
                {
                    var lineComp = lineItem.Split(" - ");

                    if (lineComp.Length != 2)
                        throw new InvalidDataException("Malformed line data provided. Please folow defined format for LockedOrder.LineItem keys");

                    var varient = lineComp[0];
                    var item = lineComp[1];
                    var price = fixedOrder?.LineItems[lineItem];

                    if (_report.SalesReport.ContainsKey(item))
                    {
                        if (_report.SalesReport[item].VarientSalesData.ContainsKey(varient))
                        {
                            _report.SalesReport[item].VarientSalesData[varient].Qty += 1;
                            _report.SalesReport[item].VarientSalesData[varient].Sales += price ?? 0.0f;
                        }
                        else
                        {
                            _report.SalesReport[item].VarientSalesData[varient] = new VarientSalesData
                            {
                                Qty = 1,
                                Sales = price ?? 0.0f
                            };
                        }
                    }
                    else
                    {
                        _report.SalesReport[item] = new SalesData
                        {
                            VarientSalesData = new Dictionary<string, VarientSalesData>
                            {
                                {
                                    varient,
                                    new VarientSalesData
                                    {
                                        Qty = 1,
                                        Sales = price ?? 0.0f
                                    }
                                },
                            }
                        };
                    }

                    if (fixedOrder?.PromoCode != null)
                    {
                        if (_report.PromoReport.VarientSalesData.ContainsKey(fixedOrder?.PromoCode ?? ""))
                        {
                            _report.PromoReport.VarientSalesData[fixedOrder?.PromoCode ?? ""].Qty += 1;
                            _report.PromoReport.VarientSalesData[fixedOrder?.PromoCode ?? ""].Sales -= fixedOrder?.Discount ?? 0.0f;
                        }
                        else
                        {
                            _report.PromoReport.VarientSalesData[fixedOrder?.PromoCode ?? ""] = new VarientSalesData
                            {
                                Qty = 1,
                                Sales = fixedOrder?.Discount ?? 0.0f
                            };
                        }
                    }
                }
            }

            return this;
        }

        public EndOfDayReportBuilder AddOrders(IQueryable<Order> orders)
        {
            var todaysOrders = orders
                .AsEnumerable()
                .Where(o => TimeZoneInfo.ConvertTimeFromUtc(o.OrderPlaced, _timezone).Date == _report.Date.Date &&
                    !string.IsNullOrEmpty(o.Locked))
                .ToList();

            return AddOrders(todaysOrders);
        }

        public EndOfDayReportBuilder SetDate(DateTime date)
        {
            _report.Date = TimeZoneInfo.ConvertTime(date, _timezone).Date;
            return this;
        }

        /// <summary>
        /// Sets the timezone of the report using the timezone ID. WARNING: Timezone defaults to UTC if the Id is not found!
        /// </summary>
        /// <param name="timezoneId">Id of the timezone you would like the report based in.</param>
        /// <returns></returns>
        public EndOfDayReportBuilder UsingTimezone(string timezoneId)
        {
            try
            {
                _timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch
            {
                _timezone = TimeZoneInfo.Utc;
            }

            return this;
        }
        public EndOfDayReportBuilder UsingTimezone(TimeZoneInfo timeZone)
        {
            _timezone = timeZone;
            return this;
        }

        public EndOfDayReport Build() => _report;
    }
}
