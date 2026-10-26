using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenOrderSystem.Core.Areas.Staff.Models.OrderTerminal;
using OpenOrderSystem.Core.Areas.Staff.ViewModels.OrderTerminal;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers;

//TODO: Set authorization through policy
[Authorize]
[Area("Staff")]
public class OrderTerminalController : Controller
{
    private static readonly string[] _stageNames = { "New", "Preparing", "Ready", "Completed" };
    private static readonly string[] _firstNames = { "James", "Mary", "Robert", "Patricia", "John", "Jennifer", "Michael", "Linda", "David", "Barbara" };
    private static readonly string[] _lastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };
    private static readonly string[] _itemNames = { "Pepperoni Pizza", "Cheese Pizza", "Veggie Pizza", "Buffalo Wings", "Garlic Breadsticks", "Caesar Salad", "2-Liter Soda" };
    private static readonly string[] _variantNames = { "Small", "Medium", "Large", "X-Large" };
    private static readonly string[] _toppingOptions = { "Extra Cheese", "Mushrooms", "Onions", "Sausage", "Black Olives", "Green Peppers" };
    private static readonly string[] _crustOptions = { "Thin Crust", "Hand Tossed", "Deep Dish", "Stuffed Crust" };
    private static readonly string[] _sauceOptions = { "Marinara", "Ranch", "BBQ", "Garlic Butter" };

    public IActionResult Index()
    {
        var vm = GetOrders(new OrderTerminalVM());

        Request.Cookies.TryGetValue("OOS_ORDERTERMINAL_ID", out var termId);
        Guid.TryParse(termId, out var termGuid);
        vm.OrderTerminalId = termId is null ? null : termGuid;

        //TODO: Perform terminalId lookup.

        return View(vm);
    }

    //TODO: Mock data - replace with real data access once the V2 order/terminal model ships.
    private OrderTerminalVM GetOrders(OrderTerminalVM vm)
    {
        var stages = _stageNames
            .Select(name => new OrderTerminalVM.StageInfo(name, new List<OrderTerminalVM.StageOrder>()))
            .ToList();

        var orderCount = Random.Shared.Next(6, 14);

        for (var i = 0; i < orderCount; i++)
        {
            var stage = stages[Random.Shared.Next(stages.Count)];
            var enteredStageAt = DateTimeOffset.Now.AddMinutes(-Random.Shared.Next(0, 45));
            stage.Orders.Add(new OrderTerminalVM.StageOrder(BuildRandomOrder(i), enteredStageAt));
        }

        vm.Stages = stages;
        return vm;
    }

    private static OrderInfoDTO BuildRandomOrder(int index)
    {
        var firstName = _firstNames[Random.Shared.Next(_firstNames.Length)];
        var lastName = _lastNames[Random.Shared.Next(_lastNames.Length)];
        var lines = BuildRandomOrderLines();
        var subTotal = lines.Sum(l => l.LinePrice);

        var adjustments = Random.Shared.Next(0, 3) == 0
            ? new List<PriceAdjustmentRecord> { new("Loyalty Discount", -Math.Round(subTotal * 0.1, 2)) }
            : new List<PriceAdjustmentRecord>();

        return new OrderInfoDTO
        {
            OrderId = 1000 + index,
            OrderConfirmationNumber = Random.Shared.Next(100000, 999999),
            CustomerName = $"{firstName} {lastName}",
            CustomerEmail = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@example.com",
            CustomerPhone = $"555-{Random.Shared.Next(100, 999)}-{Random.Shared.Next(1000, 9999)}",
            OrderPickupTime = DateTimeOffset.Now.AddMinutes(Random.Shared.Next(-10, 45)),
            OrderLines = lines,
            SubTotal = subTotal,
            PriceAdjustments = adjustments,
            Total = subTotal + adjustments.Sum(a => a.Price)
        };
    }

    private static List<OrderLineRecord> BuildRandomOrderLines()
    {
        var lineCount = Random.Shared.Next(1, 4);
        return Enumerable.Range(0, lineCount).Select(_ => BuildRandomOrderLine()).ToList();
    }

    private static OrderLineRecord BuildRandomOrderLine()
    {
        var optionsByCategory = new Dictionary<string, List<string>>
        {
            ["Toppings"] = PickRandom(_toppingOptions, 0, 3),
            ["Crust"] = PickRandom(_crustOptions, 1, 1),
            ["Sauce"] = PickRandom(_sauceOptions, 0, 1)
        }.Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

        var fullOptions = optionsByCategory.Values.SelectMany(options => options).ToList();

        // Added must be a subset of the toppings actually selected, so it's represented in
        // both OptionsListByCategory and FullOptionsList.
        var toppingsInOrder = optionsByCategory.GetValueOrDefault("Toppings", new List<string>());
        var addedOptions = toppingsInOrder.Count > 0 && Random.Shared.Next(0, 5) == 0
            ? PickRandom(toppingsInOrder.ToArray(), 1, Math.Min(2, toppingsInOrder.Count))
            : null;

        // Removed must come from toppings that were NOT selected, so it can never appear in FullOptionsList.
        var removalCandidates = _toppingOptions.Except(toppingsInOrder).ToArray();
        var removedOptions = removalCandidates.Length > 0 && Random.Shared.Next(0, 5) == 0
            ? PickRandom(removalCandidates, 1, Math.Min(2, removalCandidates.Length))
            : null;

        return new OrderLineRecord
        {
            ItemName = _itemNames[Random.Shared.Next(_itemNames.Length)],
            VariantName = _variantNames[Random.Shared.Next(_variantNames.Length)],
            FullOptionsList = fullOptions,
            OptionsListByCategory = optionsByCategory,
            RemovedOptionsList = removedOptions,
            AddedOptionsList = addedOptions,
            LinePrice = Math.Round(Random.Shared.NextDouble() * 15 + 5, 2)
        };
    }

    private static List<string> PickRandom(string[] pool, int min, int max)
    {
        var count = Random.Shared.Next(min, max + 1);
        return pool.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
    }
}