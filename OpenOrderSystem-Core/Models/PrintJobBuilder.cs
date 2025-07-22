using ESCPOS_NET.Utilities;
using OpenOrderSystem.Core.Areas.API.DTO;
using OpenOrderSystem.Core.Data.DataModels;
using PizzaPartry.tools;
using System.Text.Json;
using Twilio.Rest.Trunking.V1;

namespace OpenOrderSystem.Core.Models
{
    public class PrintJobBuilder
    {
        private readonly PrintJob _printJob = new PrintJob();
        private List<byte[]> _data = new List<byte[]>();
        private List<BuildStep> _runtimeSteps = new List<BuildStep>();
        private PrintTemplate? _template;
        private OrderResponse? _orderData;
        private EndOfDayReport? _endOfDayReport;

        public PrintJobBuilder AddInstruction(byte[] data)
        {
            _data.Add(data);
            return this;
        }

        public PrintJobBuilder UseTemplate(PrintTemplate template)
        {
            _template = template;
            _runtimeSteps = template.GetRuntimeSteps().ToList();
            return this;
        }

        public PrintJobBuilder AddOrderData(OrderResponse order)
        {
            _orderData = order;

            return this;
        }

        public PrintJobBuilder AddEndOfDayData(EndOfDayReport endOfDayReport)
        {
            _endOfDayReport = endOfDayReport;
            return this;
        }

        public PrintJobBuilder Clear()
        {
            _data.Clear();
            return this;
        }

        public PrintJob Build(string printerId)
        {
            _printJob.PrinterId = printerId;

            for (int i = 0; i < _runtimeSteps.Count; i++)
            {
                BuildStep step = _runtimeSteps[i];
                if (step.Instruction > PrintInstruction.__RUNTIME_ORDER_START__ && step.Instruction < PrintInstruction.__RUNTIME_ORDER_END__)
                    step = RuntimeDataOrderDataInjector(step);
                else if (step.Instruction > PrintInstruction.__RUNTIME_REPORT_START__ && step.Instruction < PrintInstruction.__RUNTIME_REPORT_END__)
                    step = RuntimeDataEndOfDayReportDataInjector(step);

                _runtimeSteps[i] = step;
            }

            _printJob.Data = ByteSplicer.Combine(ByteSplicer.Combine(_data.ToArray()), _template?.ProcessPrintData(_runtimeSteps.ToArray()));
            return _printJob;
        }

        private BuildStep RuntimeDataOrderDataInjector(BuildStep step)
        {
            if (_orderData == null)
            {
                step.Data = "[ ERROR: NO ORDER ]";
                return step;
            }

            switch (step.Instruction)
            {
                case PrintInstruction.PrintBarcode:
                    var algorithm = new CheckDigitCalc.WeightingFactor[]
                {
                        CheckDigitCalc.WeightingFactor.TwoMinus,
                        CheckDigitCalc.WeightingFactor.TwoMinus,
                        CheckDigitCalc.WeightingFactor.Three,
                        CheckDigitCalc.WeightingFactor.FiveMinus
                };

                    const string UPC_Prefix = "207001";
                    step.Data = _orderData.Subtotal > 99.99 ? "00000000000" : UPC_Prefix + CheckDigitCalc.Create(_orderData.Subtotal
                        .ToString("C")
                        .Replace("$", "")
                        .Replace(".", "")
                        .Replace(" ", "")
                        .PadLeft(4, '0'), algorithm)
                        .GetResult();
                    break;
                case PrintInstruction.PrintOrderLinesA:
                case PrintInstruction.PrintOrderLinesB:
                case PrintInstruction.PrintOrderLinesC:
                    step.Data = JsonSerializer.Serialize(_orderData);
                    break;
                case PrintInstruction.PrintTotal:
                    step.Data = _orderData.Total.ToString("C");
                    break;
                case PrintInstruction.PrintTax:
                    step.Data = _orderData.Tax.ToString("C");
                    break;
                case PrintInstruction.PrintSubtotal:
                    step.Data = _orderData.Subtotal.ToString("C");
                    break;
                case PrintInstruction.PrintDiscount:
                    if (_orderData.DiscountAmount != null)
                    {
                        var originalTotal = _orderData.LineItemTotal.ToString("C");
                        var discountAmnt = _orderData.DiscountAmount?.ToString("C");
                        step.Instruction = PrintInstruction.PrintGenericRunitimeLines;
                        step.Data = $"\tOriginal: {originalTotal},\tDiscount: {discountAmnt}";
                    }
                    break;
                case PrintInstruction.PrintPromoCode:
                    step.Data = _orderData.DiscountId;
                    break;
                case PrintInstruction.PrintOrderNum:
                    step.Data = _orderData.Id.ToString();
                    break;
                case PrintInstruction.PrintCustomerName:
                    step.Data = _orderData.Customer?.Name;
                    break;
                case PrintInstruction.PrintCustomerPhone:
                    var fancyAssPhoneNumber = "(";
                    var phone = _orderData.Customer?.Phone ?? "Error Retrieving Phone";

                    if (phone != "Error Retrieving Phone")
                        for (var k = 0; k < phone.Length; ++k)
                        {
                            var d = phone[k];
                            if (k == 2)
                                fancyAssPhoneNumber += $"{d})";
                            else if (k == 5)
                                fancyAssPhoneNumber += $"{d}-";
                            else
                                fancyAssPhoneNumber += d;
                        }
                    else
                        fancyAssPhoneNumber = phone;

                    step.Data = fancyAssPhoneNumber;
                    break;
            }

            return step;
        }

        private BuildStep RuntimeDataEndOfDayReportDataInjector(BuildStep step)
        {
            if (_endOfDayReport == null)
            {
                step.Instruction = PrintInstruction.PrintGenericRunitimeLines;
                step.Data = "[ JOB BUILD FAILED ],[ MISSING REPORT IN JOB BUILD ]";
            }
            else
            {
                switch (step.Instruction)
                {
                    case PrintInstruction.PrintReportDate:
                        step.Data = _endOfDayReport.Date.ToShortDateString();
                        break;
                    case PrintInstruction.PrintReportGross:
                        step.Data = _endOfDayReport.GrossSales.ToString("C");
                        break;
                    case PrintInstruction.PrintReportCustomerCount:
                        step.Data = _endOfDayReport.OrderCount.ToString();
                        break;
                    case PrintInstruction.PrintReportSales:
                        step.Data = JsonSerializer.Serialize(_endOfDayReport.SalesReport);
                        break;
                    case PrintInstruction.PrintReportPromo:
                        step.Data = JsonSerializer.Serialize(_endOfDayReport.PromoReport);
                        break;
                }
            }

            return step;
        }
    }
}
