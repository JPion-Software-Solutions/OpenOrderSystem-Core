using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.API.Legacy.DTO.Factories;
using OpenOrderSystem.Core.Areas.API.Legacy.Models;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Models;
using OpenOrderSystem.Core.Services.EmailService.Interfaces;
using OpenOrderSystem.Core.Services.Legacy;
using OpenOrderSystem.Core.Services.Sms.Interfaces;

namespace OpenOrderSystem.Core.API.Legacy.Cart;

/// <summary>Legacy <c>api/legacy/Cart</c> endpoints (formerly CartController).</summary>
public static class CartApi
{
    // PUT
    public static async Task<IResult> AddItem(
        HttpContext httpContext,
        [FromServices] CartService cartService,
        [FromServices] ApplicationDbContext context)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<CartAddItemModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        var cart = cartService.GetCart(model.CartId);
        if (cart == null)
        {
            return Results.NotFound(new
            {
                cartId = model.CartId,
                errorMessage = "Unable to locate cart! Cart may have expired."
            });
        }

        context.ProductCategories
            .Include(pc => pc.Ingredients)
            .Include(pc => pc.MenuItems)
            .Load();
        context.MenuItems
            .Include(mi => mi.Ingredients)
            .Include(mi => mi.ProductCategory)
            .Include(mi => mi.RawDbVarients)
            .Load();
        context.MenuItemVarients.Load();
        context.Ingredients
            .Include(i => i.MenuItems)
            .Load();

        var item = context.MenuItems
            .Include(mi => mi.RawDbVarients)
            .Include(mi => mi.Ingredients)
            .Include(mi => mi.ProductCategory)
            .FirstOrDefault(x => x.Id == model.ItemId);

        if (item == null)
        {
            return Results.NotFound(new
            {
                itemId = model.ItemId,
                errorMessage = "Unable to locate item in database."
            });
        }

        item.RawDbVarients = context.MenuItemVarients
            .Where(miv => miv.MenuItemId == item.Id)
            .OrderBy(miv => miv.Id)
            .ToList();

        var orderLine = new OrderLineLegacy
        {
            MenuItem = item,
            MenuItemId = model.ItemId,
            MenuItemVarient = model.Varient,
            Ingredients = item.Ingredients
        };

        cart.Order.LineItems.Add(orderLine);

        var status = cartService.UpdateCart(cart);

        if (status != CartStatus.Updated)
        {
            return Results.BadRequest(new
            {
                cartId = model.CartId,
                cartStatus = status.ToString(),
                errorMessage = $"Cart update failed. Cart service returned a status of {status}."
            });
        }

        return Results.Ok(new
        {
            cartId = model.CartId,
            itemId = model.ItemId,
            varient = model.Varient,
            itemCount = cart.Order.LineItems.Count,
            itemName = $"{item.MenuItemVarients?[orderLine.MenuItemVarient].Descriptor} -- {item.Name}",
            errorMessage = $"SUCCESS, {item.Name} added to cart {model.CartId}"
        });
    }

    // GET {cartId}/{index}
    public static async Task<IResult> ItemDetail(
        HttpContext httpContext,
        [FromServices] CartService cartService,
        [FromServices] ApplicationDbContext context)
    {
        var binder = new MvcCompatBinder(httpContext);
        var cartId = binder.RouteString("cartId", required: true)!;
        var index = binder.Route<int>("index");
        if (binder.ApiControllerError() is { } error) return error;

        var cart = cartService.GetCart(cartId);
        if (cart == null)
            return Results.NotFound($"Cart '{cartId}' could not be located.");

        if (index >= cart.Order.LineItems.Count || index < 0)
            return Results.NotFound($"Cart '{cartId}' has no line item at index '{index}'.");

        await context.IngredientCategories
            .Include(ic => ic.MemberIngredients)
            .LoadAsync();

        var line = cart.Order.LineItems[index];
        var categoryId = line.MenuItem?.ProductCategoryId ?? -1;
        var category = await context
                  .ProductCategories
                  .Include(pc => pc.Ingredients)
                  .FirstOrDefaultAsync(pc => pc.Id == categoryId);

        return Results.Ok(new
        {
            cartId,
            index,
            comments = line.LineComments,
            variantIndex = line.MenuItemVarient,
            variants = line.MenuItem?.MenuItemVarients.Select(miv =>
            {
                return new
                {
                    index = miv.Index,
                    name = miv.Descriptor,
                    price = miv.Price
                };
            }),
            ingredients = new
            {
                active = line.Ingredients?.Select(i =>
                {
                    return i.Id;
                }),

                available = category?.Ingredients?.Select(i =>
                {
                    var included = line.MenuItem?.Ingredients?.FirstOrDefault(mii => mii.Id == i.Id) != null;
                    var price = included ? 0.0f : i.Price;

                    return new
                    {
                        id = i.Id,
                        name = i.Name,
                        category = new
                        {
                            name = i.Category?.Name,
                            type = i.Category?.Type.ToString()
                        },
                        price,
                        included
                    };
                })
            }
        });
    }

    // PUT
    public static async Task<IResult> RemoveItem(HttpContext httpContext, [FromServices] CartService cartService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<CartUpdateItemModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        var cart = cartService.GetCart(model.CartId);
        if (cart == null)
        {
            return Results.NotFound(new
            {
                cartId = model.CartId,
                errorMessage = "Unable to locate cart! Cart may have expired."
            });
        }

        var itemName = "";

        if (model.Index >= 0 && model.Index < cart.Order.LineItems.Count)
        {
            itemName = cart.Order.LineItems[model.Index].ToString();
            cart.Order.LineItems.RemoveAt(model.Index);
            var status = cartService.UpdateCart(cart);

            if (status != CartStatus.Updated)
            {
                return Results.BadRequest(new
                {
                    cartId = model.CartId,
                    cartStatus = status.ToString(),
                    errorMessage = $"Cart update failed. Cart service returned a status of {status}."
                });
            }
        }
        else
        {
            return Results.BadRequest(new
            {
                cartId = model.CartId,
                index = model.Index,
                errorMessage = $"Index out of range. Cart id:{model.CartId} does not contain a line item at index# {model.Index}."
            });
        }

        return Results.Ok(new
        {
            cartId = model.CartId,
            index = model.Index,
            itemCount = cart.Order.LineItems.Count,
            errorMessage = $"SUCCESS: {itemName} successfully removed from cart id:{model.CartId}."
        });
    }

    // POST
    public static IResult New([FromServices] CartService cartService)
    {
        var newCartId = cartService.ProvisionCart();
        return Results.Ok(new
        {
            cartId = newCartId,
            errorMessage = $"SUCCESS, A new cart with the id:{newCartId} has been provisioned."
        });
    }

    // GET
    public static IResult Details(HttpContext httpContext, [FromServices] CartService cartService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var cartId = binder.QueryString("cartId", required: true)!;
        if (binder.ApiControllerError() is { } error) return error;

        MvcCompatResults.NoStore(httpContext);

        var cart = cartService.GetCart(cartId);
        if (cart == null)
        {
            return Results.NotFound(new
            {
                cartId,
                errorMessage = "Unable to locate cart! Cart may have expired."
            });
        }

        var orderLines = new List<string>();
        var lineIngredients = new List<object>();
        var orderLinesDetailed = new List<OrderLineLegacy>();

        for (int i = 0; i < cart.Order.LineItems.Count; i++)
        {
            OrderLineLegacy? line = cart.Order.LineItems[i];
            orderLines.Add(line.ToString());
            var ingredientList = new List<object>();

            foreach (var ingredient in line.Ingredients ?? new List<Ingredient>())
            {
                ingredientList.Add(new
                {
                    id = ingredient.Id,
                    name = ingredient.Name,
                    price = ingredient.Price
                });
            }

            if (line != null)
            {
                if (line.MenuItem != null)
                {
                    line.MenuItem.OrderLines = null;
                    line.MenuItem.DiscountCodesItems = null;
                    line.MenuItem.ProductCategory = null;

                    for (int j = 0; j < line.MenuItem.Ingredients?.Count; j++)
                    {
                        line.MenuItem.Ingredients[j].MenuItems = null;
                        line.MenuItem.Ingredients[j].OrderLines = null;
                        line.MenuItem.Ingredients[j].Category = null;
                        line.MenuItem.Ingredients[j].ProductCategories = null;
                    }

                    for (int j = 0; j < line.MenuItem.RawDbVarients?.Count; j++)
                    {
                        line.MenuItem.RawDbVarients[j].MenuItem = null;
                        line.MenuItem.RawDbVarients[j].DiscountCodes = null;
                    }
                }
                line.Order = null;


                for (int j = 0; j < line.Ingredients?.Count; j++)
                {
                    line.Ingredients[j].OrderLines = null;
                    line.Ingredients[j].Category = null;
                    line.Ingredients[j].ProductCategories = null;
                    line.Ingredients[j].MenuItems = null;
                }

                orderLinesDetailed.Add(line);
            }

            lineIngredients.Add(new
            {
                index = i,
                friendlyName = line?.ToString(),
                ingredientList
            });
        }

        var cartSimplified = new
        {
            id = cart.Id,
            orderLines,
            orderLinesDetailed,
            lineIngredients,
            subtotal = cart.Order.Subtotal,
            tax = cart.Order.Tax,
            total = cart.Order.Total,
            discount = cart.Promo?.GetDiscount(cart.Order),
            promoCode = cart.PromoCode,
            promoName = cart.Promo?.Name,
            itemCount = orderLines.Count,
            customer = cart.Order.Customer
        };

        return Results.Ok(new
        {
            cart = cartSimplified,
            errorMessage = $"SUCCESS: cart id:{cartId} retrieved successfully."
        });
    }

    // POST. cartId comes from the query string only; the old [FromQuery][FromBody] resolved to [FromQuery].
    public static async Task<IResult> Submit(
        HttpContext httpContext,
        [FromServices] CartService cartService,
        [FromServices] IEmailService emailService,
        [FromServices] ISmsService smsService,
        [FromServices] ApplicationDbContext context,
        [FromServices] StaffTerminalMonitoringService staffTMS,
        [FromServices] PrinterService printService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var cartId = binder.QueryString("cartId", required: true)!;
        if (binder.ApiControllerError() is { } error) return error;

        var request = httpContext.Request;

        var cart = cartService.GetCart(cartId);
        if (cart == null)
        {
            return Results.NotFound(new
            {
                cartId,
                errorMessage = "Unable to locate cart! Cart may have expired."
            });
        }

        int orderId = 0;

        if (cart.IsExistingOrder)
        {
            var ogOrder = context.Orders.FirstOrDefault(o => o.Id == cart.Order.Id) ??
                throw new ArgumentNullException();

            var lines = context.OrderLines
                .Where(li => li.OrderId == cart.Order.Id)
                .ToList();
            context.OrderLines.RemoveRange(lines);
            context.SaveChanges();

            for (int i = 0; i < cart.Order.LineItems.Count; i++)
            {
                cart.Order.LineItems[i].Id = 0;
                cart.Order.LineItems[i].MenuItem = null;
                cart.Order.LineItems[i].Order = null;

                for (int j = 0; j < cart.Order.LineItems[i].Ingredients?.Count; j++)
                {
                    var ingredient = context.Ingredients.FirstOrDefault(x => x.Id == cart.Order.LineItems[i].Ingredients[j].Id);
                    if (cart.Order.LineItems[i].Ingredients == null || ingredient == null)
                        throw new ArgumentNullException();
                    else
                        cart.Order.LineItems[i].Ingredients[j] = ingredient;
                }
            }

            var coupon = context.DiscountCodes
                .Include(d => d.Orders)
                .Include(d => d.WhiteListItemsVarients)
                .FirstOrDefault(d => d.Code == cart.PromoCode);

            var isCouponValid = coupon != null && !coupon.IsArchived;

            ogOrder.LineItems = cart.Order.LineItems;
            ogOrder.DiscountId = isCouponValid ? coupon?.Code : null;
            context.Orders.Update(ogOrder);
            context.SaveChanges();
        }
        else
        {
            if (cart.Order.Customer == null)
            {
                return Results.BadRequest(new
                {
                    cartId,
                    errorMessage = $"unable to submit cart id:{cartId}, missing customer information!"
                });
            }

            //check if customer has already placed an order today
            var existingCustomer = context.Customers
                .FirstOrDefault(c => c.Name == cart.Order.Customer.Name &&
                    c.Email == cart.Order.Customer.Email &&
                    c.Phone == cart.Order.Customer.Phone &&
                    c.CustomerCreated.Date == DateTime.UtcNow.Date);

            var customer = new Customer
            {
                Name = cart.Order.Customer.Name,
                Email = cart.Order.Customer.Email,
                Phone = cart.Order.Customer.Phone,
                SMSUpdates = cart.Order.Customer.SMSUpdates,
                EmailUpdates = cart.Order.Customer.EmailUpdates
            };

            if (existingCustomer == null)
            {
                context.Add(customer);
            }
            else
            {
                customer.Id = existingCustomer.Id;
            }

            context.SaveChanges();

            var coupon = context.DiscountCodes
                .Include(d => d.Orders)
                .Include(d => d.WhiteListItemsVarients)
                .FirstOrDefault(d => d.Code == cart.PromoCode);

            var isCouponValid = coupon != null && !coupon.IsArchived;

            var order = new Order
            {
                CustomerId = customer.Id,
                OrderPlaced = DateTime.UtcNow,
                DiscountId = isCouponValid ? cart.PromoCode : null
            };

            context.Orders.Add(order);
            context.SaveChanges();

            foreach (var line in cart.Order.LineItems)
            {
                var lineIngredients = new List<Ingredient>();

                foreach (var ingredient in line.Ingredients)
                {
                    lineIngredients.Add(context.Ingredients.First(i => i.Id == ingredient.Id));
                }

                var lineItem = new OrderLineLegacy
                {
                    Ingredients = lineIngredients,
                    LineComments = line.LineComments,
                    OrderId = order.Id,
                    MenuItemId = line.MenuItemId,
                    MenuItem = context.MenuItems.Include(mi => mi.RawDbVarients).Include(mi => mi.Ingredients).First(mi => mi.Id == line.MenuItemId),
                    MenuItemVarient = line.MenuItemVarient,
                };

                context.OrderLines.Add(lineItem);
            }

            context.SaveChanges();

            var bob = new PrintJobBuilder();

            var printer = context.Printers.FirstOrDefault(p => p.DefaultOrderPrinter);
            var template = context.PrintTemplates.FirstOrDefault(t => t.DefaultOrderTemplate);

            if (printer != null && template != null)
            {
                var job = bob
                    .AddOrderData(OrderResponseFactory.Create(order))
                    .UseTemplate(template)
                    .Build(printer.Id);

                printService.QueueJob(job);
            }

            //alert staff of new order
            staffTMS.TriggerNewOrderAlert();

            if (customer.SMSUpdates)
            {
                var phone = smsService.ConvertPhone(customer.Phone);

                var linkBuilder = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port ?? -1, $"/Order/Status/{order.Id}");
                if (linkBuilder.Uri.IsDefaultPort)
                {
                    linkBuilder.Port = -1;
                }

                var link = linkBuilder.Uri.AbsoluteUri;
                if (smsService.VerifyPhone(phone))
                {
                    smsService.SendSMS(phone, $"Thank you for ordering from the Village Market Deli in Rapid City! Your order total is {order.Total.ToString("C")}. " +
                        $"We will let you know when your order will be ready for pickup or you can check on it yourself at {link}");
                }
            }
            if (customer.EmailUpdates)
            {
                var linkBuilder = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port ?? -1, $"/Order/Status/{order.Id}");
                if (linkBuilder.Uri.IsDefaultPort)
                {
                    linkBuilder.Port = -1;
                }

                //replace email before launch!
                var link = linkBuilder.Uri.AbsoluteUri;
                //TODO Replace direct email service usage with abstracted CustomerNotificationService
                await emailService.SendAsync(customer.Email, "Thank You For Your Order", $"REPLACE BEFORE LAUNCH: Thank you for ordering from the Village Market Deli in Rapid City! Your order total is {order.Total.ToString("C")}. " +
                        $"We will let you know when your order will be ready for pickup or you can check on it yourself at {link}");
            }

            orderId = order.Id;
        }

        //remove cart from active carts
        cartService.DesposeCart(cartId);

        return Results.Ok(new
        {
            cartId,
            orderId,
            errorMessage = $"SUCCESS: cart id:{cartId} submitted."
        });
    }

    // PUT
    public static async Task<IResult> ApplyDiscount(
        HttpContext httpContext,
        [FromServices] CartService cartService,
        [FromServices] ApplicationDbContext context)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<ApplyCouponModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        context.MenuItems.Load();

        var cart = cartService.GetCart(model.CartId);

        if (cart == null)
            return Results.BadRequest("Invalid cart.");

        var coupon = context.DiscountCodes
            .Include(d => d.WhiteListItemsVarients)
            .Include(d => d.Orders)
            .FirstOrDefault(c => c.Code == model.PromoCode.ToUpper());

        if (coupon == null)
        {
            cart.PromoCode = model.PromoCode;
            return Results.BadRequest("Invalid promo code");
        }

        cart.PromoCode = model.PromoCode;
        cart.Promo = coupon;
        cartService.UpdateCart(cart);

        var discount = coupon.GetDiscount(cart.Order);

        if (discount <= 0)
            return Results.BadRequest(coupon.ErrorReason);

        return Results.Ok($"You saved {discount.ToString("C")}!");
    }

    // PUT
    public static async Task<IResult> Customer(HttpContext httpContext, [FromServices] CartService cartService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<CartCustomer>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        var cart = cartService.GetCart(model.CartId);
        if (cart == null)
        {
            return Results.NotFound(new
            {
                cartId = model.CartId,
                errorMessage = "Unable to locate cart! Cart may have expired."
            });
        }

        cart.Order.Customer = new Customer
        {
            Name = model.Name,
            Email = model.Email,
            Phone = model.Phone,
            SMSUpdates = model.SmsUpdates,
            EmailUpdates = model.EmailUpdates
        };

        var status = cartService.UpdateCart(cart);

        if (status != CartStatus.Updated)
        {
            return Results.BadRequest(new
            {
                cartId = model.CartId,
                cartStatus = status.ToString(),
                errorMessage = $"Cart update failed. Cart service returned a status of {status}."
            });
        }

        return Results.Ok(new
        {
            cartId = model.CartId,
            customer = new
            {
                name = model.Name,
                email = model.Email,
                phone = model.Phone,
                smsUpdates = model.SmsUpdates,
                emailUpdates = model.EmailUpdates
            },
            errorMessage = "SUCCESS: Customer details added to cart."
        });
    }

    // PUT
    public static async Task<IResult> UpdateItem(
        HttpContext httpContext,
        [FromServices] CartService cartService,
        [FromServices] ApplicationDbContext context)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<CartUpdateItemModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        var cart = cartService.GetCart(model.CartId);
        if (cart == null)
        {
            return Results.NotFound(new
            {
                cartId = model.CartId,
                errorMessage = "Unable to locate cart! Cart may have expired."
            });
        }

        var updateMessage = "";

        if (model.Index >= 0 && model.Index < cart.Order.LineItems.Count)
        {
            if (model.IngredientIds.Length > 0)
            {
                updateMessage = "with new ingredients list ";

                //reset ingredients trigger
                //resets the ingredients to the base item ingredient
                if (model.IngredientIds[0] == -1)
                {
                    cart.Order.LineItems[model.Index].Ingredients = cart.Order.LineItems[model.Index].MenuItem?.Ingredients ?? new List<Ingredient>();
                    updateMessage = "original ingredients from base item";
                }
                else
                {
                    var ingredients = new List<Ingredient>();
                    for (int i = 0; i < model.IngredientIds.Length; i++)
                    {
                        int id = model.IngredientIds[i];
                        var ingredient = context.Ingredients.FirstOrDefault(i => i.Id == id);

                        if (ingredient == null)
                            return Results.NotFound(new
                            {
                                cartId = model.CartId,
                                index = model.Index,
                                ingredientId = id,
                                errorMessage = $"Unable to locate ingredient with id#{id}. Please try again."
                            });

                        ingredients.Add(ingredient);
                        var start = i == 0 ? "" : ", ";
                        updateMessage += $"{start}{ingredient.Name}";
                    }

                    cart.Order.LineItems[model.Index].Ingredients = ingredients;
                }
            }

            if (!string.IsNullOrEmpty(updateMessage) && model.LineComments != null)
                updateMessage += "and ";

            if (model.LineComments != null)
                updateMessage += $"Comment: {model.LineComments}";

            if (model.VarientIndex != null)
            {
                var line = cart.LineItems[model.Index];
                if (line.MenuItem != null)
                {
                    line.MenuItem.Varient = model.VarientIndex ?? 0;
                    line.MenuItemVarient = line.MenuItem.Varient;
                    updateMessage += $" varient changed to {line.MenuItem.MenuItemVarients?[line.MenuItem.Varient].Descriptor}";
                }
            }

            cart.Order.LineItems[model.Index].LineComments = model.LineComments;
            var status = cartService.UpdateCart(cart);

            if (status != CartStatus.Updated)
            {
                return Results.BadRequest(new
                {
                    cartId = model.CartId,
                    cartStatus = status.ToString(),
                    errorMessage = $"Cart update failed. Cart service returned a status of {status}."
                });
            }
        }
        else
        {
            return Results.BadRequest(new
            {
                cartId = model.CartId,
                index = model.Index,
                ingredientIds = model.IngredientIds,
                errorMessage = $"Index out of range. Cart id:{model.CartId} does not contain a line item at index# {model.Index}."
            });
        }

        return Results.Ok(new
        {
            cartId = model.CartId,
            index = model.Index,
            ingredientIds = model.IngredientIds,
            errorMessage = $"SUCCESS: updated line {model.Index} of cart id:{model.CartId} {updateMessage}."
        });
    }

    // DELETE. cartId comes from the "cartId" request header.
    public static IResult Dispose(HttpContext httpContext, [FromServices] CartService cartService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var cartId = binder.HeaderString("cartId", required: true)!;
        if (binder.ApiControllerError() is { } error) return error;

        var status = cartService.DesposeCart(cartId);
        switch (status)
        {
            case CartStatus.NotFound:
                return Results.NotFound(new
                {
                    cartId,
                    errorMessage = $"The cart with id:{cartId} could not be located."
                });
            case CartStatus.Expired:
                return Results.NotFound(new
                {
                    cartId,
                    errorMessage = $"The cart with id:{cartId} has been marked as either expired or disposed."
                });
            default:
                break;
        }

        return Results.Ok(new
        {
            cartId,
            errorMessage = $"SUCCESS: cart id:{cartId} has been removed."
        });
    }
}
