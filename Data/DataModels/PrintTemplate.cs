
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using ImageMagick;
using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.IdentityModel.Tokens;
using OpenOrderSystem.Core.Areas.API.DTO;
using OpenOrderSystem.Core.Areas.Staff.Models;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Models;
using SixLabors.ImageSharp.Formats.Bmp;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class PrintTemplate
    {
        private List<BuildStep> _steps = new List<BuildStep>();

        /// <summary>
        /// GUID used to specifically identify this print template.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Common name used to identify this PrintTemplate
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the JSON serialized array of the BuildSteps required for the PrintTemplate
        /// </summary>
        public string BuildInstructions
        {
            get
            {
                return JsonSerializer.Serialize(_steps);
            }
            set
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() } // This ensures enums are deserialized by numeric values
                    };

                    _steps = JsonSerializer.Deserialize<List<BuildStep>>(value, options) ?? new List<BuildStep>();
                }
                catch (JsonException ex)
                {
                    _steps =
                    [
                        new BuildStep
                        {
                            Instruction = PrintInstruction.__ERROR__,
                            Data = ex.Message,
                        },
                    ];
                }
            }
        }

        /// <summary>
        /// Not mapped to database: Gets the existing build steps in the PrintTemplate.
        /// </summary>
        [NotMapped]
        public List<BuildStep> Instructions => _steps;

        public bool DefaultOrderTemplate { get; set; } = false;

        public bool DefaultEndOfDayTemplate { get; set; } = false;

        /// <summary>
        /// Adds a build step to the template
        /// </summary>
        /// <param name="instruction">BuildStep instruction type</param>
        /// <param name="data">Data required to carry out the instruction</param>
        /// <returns>modified PrintTemplate</returns>
        public PrintTemplate AddBuildStep(PrintInstruction instruction, string? data = null)
        {
            _steps.Add(new BuildStep
            {
                Instruction = instruction,
                Data = data
            });

            return this;
        }

        /// <summary>
        /// Removes an existing build step from the template
        /// </summary>
        /// <param name="Id">Id of the build step to be removed.</param>
        /// <returns>Existing print template with the BuildStep removed</returns>
        public PrintTemplate RemoveBuildStep(string Id)
        {
            var step = _steps.FirstOrDefault(x => x.Id == Id);

            if (step.Id == Id)
            {
                _steps.Remove(step);
            }

            return this;
        }

        /// <summary>
        /// Turns the build steps of the template into actual print instructions.
        /// </summary>
        /// <param name="runtimeBuildSteps">Array of runtime build steps that will replace the runtime data placeholders.</param>
        /// <returns>Byte array containing the final instructions to be sent to the printer.</returns>
        public byte[] ProcessPrintData(BuildStep[] runtimeBuildSteps)
        {
            //substitute placeholder data for runtime data
            foreach (var rtStep in runtimeBuildSteps)
            {
                var step = _steps.FirstOrDefault(s => s.Id == rtStep.Id);
                var index = _steps.IndexOf(step);

                _steps[index] = rtStep;
            }

            List<byte[]> data = new List<byte[]>();

            foreach (var step in _steps)
            {
                data.Add(BuildStep(step));
            }

            return ByteSplicer.Combine(data.ToArray());
        }

        /// <summary>
        /// Retrieves an array of the runtime build steps so that the runtime data can be injected.
        /// </summary>
        /// <returns>BuildStep array containing the build steps that fall in the "runtime steps" category of build steps.</returns>
        public BuildStep[] GetRuntimeSteps() => _steps
            .Where(s => s.Instruction > PrintInstruction.__RUNTIME_CMD_START && s.Instruction < PrintInstruction.__RUNTIME_CMD_END__)
            .ToArray();

        /// <summary>
        /// Retrieve the print instructions for a single BuildStep
        /// </summary>
        /// <param name="step">BuildStep to process</param>
        /// <returns>byte array containing the printer instructions (ESC/POS) for a single step in the print process.</returns>
        protected byte[] BuildStep(BuildStep step)
        {
            var data = new byte[0];
            var e = new EPSON();

            if (step.Instruction > PrintInstruction.__FORMATING_CMD_START__ && step.Instruction < PrintInstruction.__FORMATTING_CMD_END__)
                data = BuildFormatStep(step);

            else if (step.Instruction > PrintInstruction.__PRINT_CMD_START && step.Instruction < PrintInstruction.__PRINT_CMD_END__)
                data = BuildPrintStep(step);

            else if (step.Instruction > PrintInstruction.__RUNTIME_CMD_START && step.Instruction < PrintInstruction.__RUNTIME_CMD_END__)
                data = BuildRuntimeStep(step);

            else if (step.Instruction > PrintInstruction.__FUNCTION_CMD_START && step.Instruction < PrintInstruction.__FUNCTION_CMD_END__)
                data = BuildFunctionStep(step);

            return data;
        }

        /// <summary>
        /// Contains the processing logic for any Format Command build step
        /// </summary>
        /// <param name="step">Buildstep to process</param>
        /// <returns>byte array containing the printer instruction (ESC/POS) for a single step in the print process.</returns>
        private byte[] BuildFormatStep(BuildStep step)
        {
            var data = Array.Empty<byte>();
            var e = new EPSON();

            switch (step.Instruction)
            {
                //Formatting Commands
                case PrintInstruction.HeaderText1:
                    data = ByteSplicer.Combine(
                        e.SetStyles(PrintStyle.DoubleWidth | PrintStyle.DoubleHeight | PrintStyle.Bold),
                        e.CenterAlign());
                    break;
                case PrintInstruction.HeaderText2:
                    data = ByteSplicer.Combine(
                        e.SetStyles(PrintStyle.DoubleHeight),
                        e.CenterAlign());
                    break;
                case PrintInstruction.LargeText:
                    data = ByteSplicer.Combine(
                        e.SetStyles(PrintStyle.DoubleWidth),
                        e.SetStyles(PrintStyle.DoubleHeight));
                    break;
                case PrintInstruction.NormalText:
                    data = e.SetStyles(PrintStyle.None);
                    break;
                case PrintInstruction.AlignCenter:
                    data = e.CenterAlign();
                    break;
                case PrintInstruction.AlignRight:
                    data = e.RightAlign();
                    break;
                case PrintInstruction.AlignLeft:
                    data = e.LeftAlign();
                    break;
            }

            return data;
        }

        /// <summary>
        /// Contains the processing logic for any Static Print Command build step
        /// </summary>
        /// <param name="step">Buildstep to process</param>
        /// <returns>byte array containing the printer instruction (ESC/POS) for a single step in the print process.</returns>
        private byte[] BuildPrintStep(BuildStep step)
        {
            var data = Array.Empty<byte>();
            var e = new EPSON();

            switch (step.Instruction)
            {
                case PrintInstruction.PrintFormatLine:
                    {
                        var root = JsonDocument.Parse(step.Data ?? "{ }").RootElement;
                        root.TryGetProperty("format", out var formatElm);
                        root.TryGetProperty("repeat", out var repeatElm);

                        var format = formatElm.ToString();
                        int.TryParse(repeatElm.ToString(), out var repeat);

                        var line = "";
                        for (int i = 0; i < repeat; ++i)
                        {
                            line += format;
                        }

                        data = e.PrintLine(line);
                    }
                    break;
                case PrintInstruction.PrintStaticText:
                    data = e.PrintLine(step.Data);
                    break;
                case PrintInstruction.PrintAllLines:
                    {
                        var lines = step.Data?.Split(',') ?? Array.Empty<string>();
                        foreach (var line in lines) data = ByteSplicer.Combine(data, e.PrintLine(line));
                    }
                    break;
                case PrintInstruction.PrintImage:
                    step.Data = step.Data?.Replace("\\media\\", "");
                    if (step.Data == null || !File.Exists(Path.Combine(MediaManagerService.MediaRootPath, step.Data)))
                    {
                        data = e.PrintLine("[ ERROR LOADING IMAGE ]");
                    }
                    else
                    {
                        var image = new MagickImage(Path.Combine(MediaManagerService.MediaRootPath, step.Data));
                        var imageInfo = new MagickImageInfo(Path.Combine(MediaManagerService.MediaRootPath, step.Data));

                        uint maxWidth = 250;

                        // Only resize if the width is greater than maxWidth
                        if (image.Width > maxWidth)
                        {
                            uint newHeight = (uint)(image.Height * (maxWidth / (double)image.Width));
                            image.Resize(maxWidth, newHeight);
                        }

                        // Convert to grayscale
                        image.ColorType = ColorType.Grayscale;

                        string tempDir = Path.Combine(MediaManagerService.MediaRootPath, "temp");
                        string tempImg = Path.Combine(tempDir, $"{DateTime.UtcNow.Ticks}.bmp");

                        image.Write(tempImg);

                        var imageData = File.ReadAllBytes(tempImg);

                        data = ByteSplicer.Combine(
                            e.FeedLines(1),
                            e.PrintImage(imageData, false),
                            e.FeedLines(1));
                    }

                    break;
            }

            return data;
        }

        /// <summary>
        /// Contains the processing logic for any Dynamic Print Command build step
        /// </summary>
        /// <param name="step">Buildstep to process</param>
        /// <returns>byte array containing the printer instruction (ESC/POS) for a single step in the print process.</returns>
        private byte[] BuildRuntimeStep(BuildStep step)
        {
            var data = Array.Empty<byte>();
            var e = new EPSON();

            switch (step.Instruction)
            {
                case PrintInstruction.PrintGenericRunitimeLines:
                    {
                        var lines = step.Data?.Split(',') ?? Array.Empty<string>();
                        foreach (var line in lines)
                        {
                            data = ByteSplicer.Combine(data, e.PrintLine(line));
                        }
                    }
                    break;
                case PrintInstruction.PrintBarcode:
                    data = ByteSplicer.Combine(
                        e.CenterAlign(),
                        e.PrintBarcode(BarcodeType.UPC_A, step.Data),
                        e.LeftAlign());
                    break;
                case PrintInstruction.PrintOrderLinesA:
                case PrintInstruction.PrintOrderLinesB:
                case PrintInstruction.PrintOrderLinesC:
                    try
                    {
                        var order = JsonSerializer.Deserialize<OrderResponse>(step.Data ?? string.Empty);

                        if (order == null)
                        {
                            data = e.PrintLine("[ Error: Missing Order Data ]\n[ ORDER LINE ITEMS HERE ]");
                        }
                        else if (step.Instruction == PrintInstruction.PrintOrderLinesA)
                        {
                            foreach (var line in order.LineItems)
                            {
                                data = ByteSplicer.Combine(
                                    data,
                                    e.SetStyles(PrintStyle.Bold),
                                    e.PrintLine($"{line.MenuItem?.MenuItemVarients[line.MenuItemVarient].Descriptor} - {line.MenuItem?.Name}"),
                                    e.SetStyles(PrintStyle.None));

                                if (line.AddedIngredients != null && line.AddedIngredients.Any())
                                {
                                    data = ByteSplicer.Combine(data, e.PrintLine($"\tAdd:"));

                                    foreach (var addition in line.AddedIngredients)
                                    {
                                        data = ByteSplicer.Combine(data, e.PrintLine($"\t{addition.Name} +{addition.Price.ToString("C")}"));
                                    }
                                }

                                if (line.RemovedIngredients != null && line.RemovedIngredients.Any())
                                {
                                    data = ByteSplicer.Combine(data, e.PrintLine($"\tRemove:"));

                                    foreach (var subtraction in line.RemovedIngredients)
                                    {
                                        data = ByteSplicer.Combine(data, e.PrintLine($"\t{subtraction.Name}"));
                                    }
                                }

                                if (line.LineComments != null)
                                {
                                    data = ByteSplicer.Combine(data, e.PrintLine(line.LineComments));
                                }

                                data = ByteSplicer.Combine(
                                    data,
                                    e.RightAlign(),
                                    e.SetStyles(PrintStyle.Bold),
                                    e.PrintLine(line.LinePrice.ToString("C")),
                                    e.SetStyles(PrintStyle.None),
                                    e.LeftAlign());
                            }
                        }
                        else if (step.Instruction == PrintInstruction.PrintOrderLinesB)
                        {

                        }
                        else
                        {
                            var lineData = new List<byte[]> { e.LeftAlign() };

                            foreach (var line in order.LineItems)
                            {
                                lineData.Add(ByteSplicer.Combine(
                                    e.SetStyles(PrintStyle.Bold),
                                    e.FeedLines(1),
                                    e.PrintLine($"       Item: {line.MenuItem?.Name}"),
                                    e.PrintLine($"    Varient: {line.MenuItem?.MenuItemVarients[line.MenuItemVarient].Descriptor}"),
                                    e.PrintLine($"   Comments: {line.LineComments ?? "N/A"}"),
                                    e.PrintLine($"\nFull Ingredient List:"),
                                    e.SetStyles(PrintStyle.None)));

                                foreach (var ingredient in line.Ingredients ?? new List<Ingredient>())
                                {
                                    lineData.Add(e.PrintLine($"\t{ingredient.Name}"));
                                }

                                lineData.Add(ByteSplicer.Combine(
                                    e.SetStyles(PrintStyle.Bold),
                                    e.PrintLine("Added Ingredients:"),
                                    e.SetStyles(PrintStyle.None)));

                                if (line.AddedIngredients?.Count == 0)
                                    lineData.Add(e.PrintLine("\tN/A"));

                                foreach (var ingredient in line.AddedIngredients ?? new List<Ingredient>())
                                    lineData.Add(e.PrintLine($"\t{ingredient.Name}"));

                                lineData.Add(ByteSplicer.Combine(
                                    e.SetStyles(PrintStyle.Bold),
                                    e.PrintLine("Removed Ingredients:"),
                                    e.SetStyles(PrintStyle.None)));

                                if (line.RemovedIngredients?.Count == 0)
                                    lineData.Add(e.PrintLine("\tN/A"));

                                foreach (var ingredient in line.RemovedIngredients ?? new List<Ingredient>())
                                    lineData.Add(e.PrintLine($"\t{ingredient.Name}"));
                            }

                            data = ByteSplicer.Combine(lineData.ToArray());
                        }

                    }
                    catch (JsonException)
                    {
                        data = e.PrintLine("[ ORDER LINE ITEMS HERE ]");
                    }
                    break;
                case PrintInstruction.PrintTotal:
                    data = ByteSplicer.Combine(e.LeftAlign(),
                        e.PrintLine($"\t   Total: {step.Data}"));
                    break;
                case PrintInstruction.PrintTax:
                    data = ByteSplicer.Combine(e.LeftAlign(),
                        e.PrintLine($"\t     Tax: {step.Data}"));
                    break;
                case PrintInstruction.PrintSubtotal:
                    data = ByteSplicer.Combine(e.LeftAlign(),
                        e.PrintLine($"\tSubtotal: {step.Data}"));
                    break;
                case PrintInstruction.PrintDiscount:
                    if (step.Data.IsNullOrEmpty())
                        break;
                    data = ByteSplicer.Combine(
                        e.LeftAlign(),
                        e.PrintLine(step.Data));
                    break;
                case PrintInstruction.PrintPromoCode:
                    if (step.Data.IsNullOrEmpty())
                        break;
                    data = ByteSplicer.Combine(
                        e.LeftAlign(),
                        e.PrintLine($"\t   Promo: {step.Data}"));
                    break;
                case PrintInstruction.PrintOrderNum:
                    data = e.PrintLine($"Order #: {step.Data}");
                    break;
                case PrintInstruction.PrintCustomerName:
                    data = e.PrintLine($"Customer: {step.Data}");
                    break;
                case PrintInstruction.PrintCustomerPhone:
                    data = e.PrintLine($"   Phone: {step.Data}");
                    break;
                case PrintInstruction.PrintReportDate:
                    if (!step.Data.IsNullOrEmpty())
                    {
                        data = e.PrintLine($"     Date: {step.Data}");
                    }
                    break;
                case PrintInstruction.PrintReportCustomerCount:
                    if (!step.Data.IsNullOrEmpty())
                    {
                        data = e.PrintLine($"Customers: {step.Data}");
                    }
                    break;
                case PrintInstruction.PrintReportGross:
                    if (!step.Data.IsNullOrEmpty())
                    {
                        data = e.PrintLine($"    Sales: {step.Data}");
                    }
                    break;
                case PrintInstruction.PrintReportSales:
                    if (!step.Data.IsNullOrEmpty())
                    {
                        var sales = JsonSerializer.Deserialize<Dictionary<string, SalesData>>(step.Data!);
                        foreach (var product in sales?.Keys ?? new Dictionary<string, SalesData>().Keys)
                        {
                            data = ByteSplicer.Combine(data,
                                e.SetStyles(PrintStyle.Bold),
                                e.PrintLine($"\n{product}"),
                                e.PrintLine($"     Qty: {sales?[product].Qty}"),
                                e.PrintLine($"   Gross: {sales?[product].Sales.ToString("C")}"),
                                e.SetStyles(PrintStyle.None));

                            foreach (var varient in sales?[product].VarientSalesData.Keys ?? new Dictionary<string, VarientSalesData>().Keys)
                            {
                                data = ByteSplicer.Combine(data,
                                    e.PrintLine($"\t{sales?[product].VarientSalesData[varient].Qty}x {varient}: {sales?[product].VarientSalesData[varient].Sales.ToString("C")}"));
                            }
                        }
                    }
                    break;
                case PrintInstruction.PrintReportPromo:
                    if (!step.Data.IsNullOrEmpty())
                    {
                        var promos = JsonSerializer.Deserialize<SalesData>(step.Data!);
                        data = ByteSplicer.Combine(
                            e.SetStyles(PrintStyle.Bold),
                            e.PrintLine($"Redeemed Promos: {promos?.Qty}"),
                            e.PrintLine($" Discount Total: {promos?.Sales.ToString("C")}"),
                            e.SetStyles(PrintStyle.None));

                        foreach (var promo in promos?.VarientSalesData.Keys ?? new Dictionary<string, VarientSalesData>().Keys)
                        {
                            data = ByteSplicer.Combine(data,
                                e.SetStyles(PrintStyle.Bold),
                                e.Print($"\n{promo}"),
                                e.SetStyles(PrintStyle.None),
                                e.Print($" x{promos?.VarientSalesData[promo].Qty}: {promos?.VarientSalesData[promo].Sales.ToString("C")}"));
                        }
                    }
                    break;
            }

            return data;
        }

        /// <summary>
        /// Contains the processing logic for any Function Command build step
        /// </summary>
        /// <param name="step">Buildstep to process</param>
        /// <returns>byte array containing the printer instruction (ESC/POS) for a single step in the print process.</returns>
        private byte[] BuildFunctionStep(BuildStep step)
        {
            var data = Array.Empty<byte>();
            var e = new EPSON();

            switch (step.Instruction)
            {
                case PrintInstruction.FeedLine:
                    data = e.FeedLines(1);
                    break;
                case PrintInstruction.FeedLines:
                    {
                        int.TryParse(step.Data, out int printLines);
                        printLines = printLines == 0 ? 1 : printLines;
                        data = e.FeedLines(printLines);
                    }
                    break;
                case PrintInstruction.CutPaper:
                    data = e.PartialCut();
                    break;
                case PrintInstruction.CutPaperFull:
                    data = e.FullCut();
                    break;
                case PrintInstruction.CutPaperFeed:
                    {
                        int.TryParse(step.Data, out int printLines);
                        printLines = printLines == 0 ? 1 : printLines;
                        data = e.PartialCutAfterFeed(printLines);
                    }
                    break;
                case PrintInstruction.CutPaperFullFeed:
                    {
                        int.TryParse(step.Data, out int printLines);
                        printLines = printLines == 0 ? 1 : printLines;
                        data = e.FullCutAfterFeed(printLines);
                    }
                    break;
            }

            return data;
        }
    }

    public struct BuildStep
    {
        public BuildStep()
        {
            Id = Guid.NewGuid().ToString();
        }

        public string Id { get; private set; }
        public PrintInstruction Instruction { get; set; }
        public string? Data { get; set; }
    }

    public enum PrintInstruction
    {
        /// <summary>
        /// INVALID COMMAND: This is used as a placeholder to mark where the commands related to formatting start
        /// </summary>
        __FORMATING_CMD_START__ = 100,

        HeaderText1,
        HeaderText2,
        LargeText,
        NormalText,
        AlignCenter,
        AlignRight,
        AlignLeft,

        /// <summary>
        /// INVALID COMMAND: This is used as a placeholder to mark where the commands related to formatting end
        /// </summary>
        __FORMATTING_CMD_END__,

        /// <summary>
        /// INVALID COMMAND: This is used as a placeholder to mark where the commands related to printing start
        /// </summary>
        __PRINT_CMD_START = 200,

        PrintFormatLine,
        PrintStaticText,
        PrintAllLines,
        PrintImage,

        /// <summary>
        /// INVALID COMMAND: This is used as a placeholder to mark where the commands related to printing end
        /// </summary>
        __PRINT_CMD_END__,

        /// <summary>
        /// INVALID COMMAND: This is used as a placeholder to mark where the commands related to printing start
        /// </summary>
        __RUNTIME_CMD_START = 300,

        PrintGenericRunitimeLines,
        PrintTodaysDate,

        __RUNTIME_ORDER_START__ = 400,

        PrintBarcode,
        PrintOrderLinesA,
        PrintOrderLinesB,
        PrintOrderLinesC,
        PrintTotal,
        PrintTax,
        PrintSubtotal,
        PrintDiscount,
        PrintPromoCode,
        PrintOrderNum,
        PrintCustomerName,
        PrintCustomerPhone,
        PrintOrderDate,

        __RUNTIME_ORDER_END__,

        __RUNTIME_REPORT_START__ = 500,

        PrintReportDate,
        PrintReportSales,
        PrintReportPromo,
        PrintReportGross,
        PrintReportCustomerCount,

        __RUNTIME_REPORT_END__,

        /// <summary>
        /// INVALID COMMAND: This is used as a placeholder to mark where the commands related to printing end
        /// </summary>
        __RUNTIME_CMD_END__,

        /// <summary>
        /// INVALID COMMAND: This is used as a placeholder to mark where the commands related to printer functions start
        /// </summary>
        __FUNCTION_CMD_START = 600,

        FeedLine,
        FeedLines,
        CutPaper,
        CutPaperFull,
        CutPaperFeed,
        CutPaperFullFeed,

        /// <summary>
        /// INVALID COMMAND: This is used as a placeholder to mark where the commands related to printer functions end
        /// </summary>
        __FUNCTION_CMD_END__,

        __ERROR__
    }
}
