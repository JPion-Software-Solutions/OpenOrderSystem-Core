using OpenOrderSystem.Core.Areas.API.DTO;
using OpenOrderSystem.Core.Data.DataModels;

namespace OpenOrderSystem.Core.Areas.API.DTO.Factories
{
    public class OrderResponseFactory
    {

        public static OrderResponse Create(Order order)
        {
            var response = new OrderResponse
            {
                Id = order.Id,
                OrderPlaced = order.OrderPlaced,
                OrderInprogress = order.OrderInprogress,
                OrderReady = order.OrderReady,
                OrderComplete = order.OrderComplete,
                Customer = order.Customer,
                MinutesToReady = order.MinutesToReady,
                LineItemTotal = order.LineItemTotal,
                Subtotal = order.Subtotal,
                Tax = order.Tax,
                Total = order.Total,
                DiscountId = order.DiscountId,
                Stage = order.Stage,
                LineItems = order.LineItems?.Select(ol => new OrderLine
                {
                    Id = ol.Id,
                    OrderId = ol.OrderId,
                    MenuItemVarient = ol.MenuItemVarient,
                    Ingredients = ol.Ingredients?.Select(i => new Ingredient
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Price = i.Price
                    }).ToList(),
                    LineComments = ol.LineComments,
                    MenuItemId = ol.MenuItemId
                }).ToList() ?? new List<OrderLine>()
            };

            for (int i = 0; i < response.LineItems.Count; i++)
            {
                response.LineItems[i].MenuItem = order.LineItems?[i].MenuItem;

                if (response.LineItems[i].MenuItem != null)
                {
                    response.LineItems[i].MenuItem!.OrderLines = null;
                    response.LineItems[i].MenuItem!.Ingredients = response.LineItems[i]?.MenuItem?.Ingredients?.Select(m => new Ingredient
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Price = m.Price,
                        CategoryId = m.CategoryId
                    })?.ToList();

                    response.LineItems[i].MenuItem!.ProductCategory = new ProductCategory
                    {
                        Id = response.LineItems[i].MenuItem?.ProductCategoryId ?? -1,
                        Name = response.LineItems[i].MenuItem?.Name ?? string.Empty,
                        Description = response.LineItems[i]?.MenuItem?.Description ?? string.Empty,
                    };

                    response.LineItems[i].MenuItem!.RawDbVarients = (response.LineItems[i].MenuItem?.RawDbVarients?.Select(v => new MenuItemVarient
                    {
                        Id = v.Id,
                        Descriptor = v.Descriptor,
                        Index = v.Index,
                        MenuItemId = v.MenuItemId,
                        Price = v.Price,
                        Priority = v.Priority,
                        Upc = v.Upc
                    }))?.ToList();

                    response.LineItems[i].Ingredients = response.LineItems[i]?.Ingredients?.Select(i => new Ingredient
                    {
                        Id = i.Id,
                        CategoryId = i.CategoryId,
                        Price = i.Price,
                        Name = i.Name
                    })?.ToList();
                }
            }

            if (order.Discount != null)
            {
                response.DiscountAmount = order.Discount.GetDiscount(order, true);
                response.DiscountId = order.DiscountId;
            }

            return response;
        }
    }
}
