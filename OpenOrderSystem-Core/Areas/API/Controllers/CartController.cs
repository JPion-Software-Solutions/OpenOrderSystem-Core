using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using OpenOrderSystem.Core.Areas.API.DTO.Factories;
using OpenOrderSystem.Core.Areas.API.Models;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Models;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Services.Interfaces;
using System.Linq.Expressions;

namespace OpenOrderSystem.Core.Areas.API.Controllers
{
    [Area("API")]
    [ApiController]
    [Route("API/Cart/{action}")]
    public class CartController : ControllerBase
    {
        private readonly CartService _cartService;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CartController> _logger;
        private readonly StaffTerminalMonitoringService _staffTMS;
        private readonly PrinterService _printService;

        public CartController(CartService cartService, IEmailService emailService, ISmsService smsService,
            ApplicationDbContext context, ILogger<CartController> logger, StaffTerminalMonitoringService staffTMS, PrinterService printService)
        {
            _cartService = cartService;
            _emailService = emailService;
            _smsService = smsService;
            _context = context;
            _logger = logger;
            _staffTMS = staffTMS;
            _printService = printService;
        }

        [HttpPut]
        public IResult AddItem([FromBody][FromHeader][FromForm] CartAddItemModel model)
        {
            if (!ModelState.IsValid)
            {
                return Results.BadRequest(new
                {
                    model,
                    modelState = ModelState,
                    errorMessage = "Invalid ModelState, please refer to model and modelState for more information."
                });
            }

            var cart = _cartService.GetCart(model.CartId);
            if (cart == null)
            {
                return Results.NotFound(new
                {
                    cartId = model.CartId,
                    errorMessage = "Unable to locate cart! Cart may have expired."
                });
            }

            _context.ProductCategories
                .Include(pc => pc.Ingredients)
                .Include(pc => pc.MenuItems)
                .Load();
            //_context.OrderLines
            //    .Include(ol => ol.Ingredients)
            //    .Load();
            _context.MenuItems
                .Include(mi => mi.Ingredients)
                .Include(mi => mi.ProductCategory)
                .Include(mi => mi.RawDbVarients)
                .Load();
            _context.MenuItemVarients.Load();
            _context.Ingredients
                .Include(i => i.MenuItems)
                .Load();

            var item = _context.MenuItems
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

            item.RawDbVarients = _context.MenuItemVarients
                .Where(miv => miv.MenuItemId == item.Id)
                .OrderBy(miv => miv.Id)
                .ToList();

            var orderLine = new OrderLine
            {
                MenuItem = item,
                MenuItemId = model.ItemId,
                MenuItemVarient = model.Varient,
                Ingredients = item.Ingredients
            };

            cart.Order.LineItems.Add(orderLine);

            var status = _cartService.UpdateCart(cart);

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

        [HttpPut]
        public IResult RemoveItem([FromBody] CartUpdateItemModel model)
        {
            if (!ModelState.IsValid)
            {
                return Results.BadRequest(new
                {
                    model,
                    modelState = ModelState,
                    errorMessage = "Invalid ModelState, please refer to model and modelState for more information."
                });
            }

            var cart = _cartService.GetCart(model.CartId);
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
                var status = _cartService.UpdateCart(cart);

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

        [HttpGet]
        public IResult New()
        {
            var newCartId = _cartService.ProvisionCart();
            return Results.Ok(new
            {
                cartId = newCartId,
                errorMessage = $"SUCCESS, A new cart with the id:{newCartId} has been provisioned."
            });
        }

        [HttpGet]
        public IResult Details([FromQuery] string cartId)
        {
            var cart = _cartService.GetCart(cartId);
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

            for (int i = 0; i < cart.Order.LineItems.Count; i++)
            {
                OrderLine? line = cart.Order.LineItems[i];
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

                lineIngredients.Add(new
                {
                    index = i,
                    friendlyName = line.ToString(),
                    ingredientList
                });
            }

            var cartSimplified = new
            {
                id = cart.Id,
                orderLines,
                lineIngredients,
                itemCount = orderLines.Count,
                customer = cart.Order.Customer
            };

            return Results.Ok(new
            {
                cart = cartSimplified,
                errorMessage = $"SUCCESS: cart id:{cartId} retrieved successfully."
            });
        }

        [HttpPost]
        public IResult Submit([FromQuery][FromBody] string cartId)
        {
            if (!ModelState.IsValid)
            {
                return Results.BadRequest(new
                {
                    cartId,
                    modelState = ModelState,
                    errorMessage = "Invalid ModelState, please refer to model and modelState for more information."
                });
            }

            if (cartId == null)
            {
                return Results.BadRequest(new
                {
                    cartId,
                    errorMessage = "Null cart id."
                });
            }

            var cart = _cartService.GetCart(cartId);
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
                var ogOrder = _context.Orders.FirstOrDefault(o => o.Id == cart.Order.Id) ??
                    throw new ArgumentNullException();

                var lines = _context.OrderLines
                    .Where(li => li.OrderId == cart.Order.Id)
                    .ToList();
                _context.OrderLines.RemoveRange(lines);
                _context.SaveChanges();

                for (int i = 0; i < cart.Order.LineItems.Count; i++)
                {
                    cart.Order.LineItems[i].Id = 0;
                    cart.Order.LineItems[i].MenuItem = null;
                    cart.Order.LineItems[i].Order = null;

                    for (int j = 0; j < cart.Order.LineItems[i].Ingredients?.Count; j++)
                    {
                        var ingredient = _context.Ingredients.FirstOrDefault(x => x.Id == cart.Order.LineItems[i].Ingredients[j].Id);
                        if (cart.Order.LineItems[i].Ingredients == null || ingredient == null)
                            throw new ArgumentNullException();
                        else
                            cart.Order.LineItems[i].Ingredients[j] = ingredient;
                    }
                }

                var coupon = _context.DiscountCodes
                    .Include(d => d.Orders)
                    .Include(d => d.WhiteListItemsVarients)
                    .FirstOrDefault(d => d.Code == cart.PromoCode);

                var isCouponValid = coupon != null && !coupon.IsArchived;

                ogOrder.LineItems = cart.Order.LineItems;
                ogOrder.DiscountId = isCouponValid ? coupon?.Code : null;
                _context.Orders.Update(ogOrder);
                _context.SaveChanges();
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
                var existingCustomer = _context.Customers
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
                    _context.Add(customer);
                }
                else
                {
                    customer.Id = existingCustomer.Id;
                }

                _context.SaveChanges();

                var coupon = _context.DiscountCodes
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

                _context.Orders.Add(order);
                _context.SaveChanges();

                foreach (var line in cart.Order.LineItems)
                {
                    var lineIngredients = new List<Ingredient>();

                    foreach (var ingredient in line.Ingredients)
                    {
                        lineIngredients.Add(_context.Ingredients.First(i => i.Id == ingredient.Id));
                    }

                    var lineItem = new OrderLine
                    {
                        Ingredients = lineIngredients,
                        LineComments = line.LineComments,
                        OrderId = order.Id,
                        MenuItemId = line.MenuItemId,
                        MenuItem = _context.MenuItems.Include(mi => mi.RawDbVarients).Include(mi => mi.Ingredients).First(mi => mi.Id == line.MenuItemId),
                        MenuItemVarient = line.MenuItemVarient,
                    };

                    _context.OrderLines.Add(lineItem);
                }

                _context.SaveChanges();

                var bob = new PrintJobBuilder();

                var printer = _context.Printers.FirstOrDefault(p => p.DefaultOrderPrinter);
                var template = _context.PrintTemplates.FirstOrDefault(t => t.DefaultOrderTemplate);

                if (printer != null && template != null)
                {
                    var job = bob
                        .AddOrderData(OrderResponseFactory.Create(order))
                        .UseTemplate(template)
                        .Build(printer.Id);

                    _printService.QueueJob(job);
                }

                //alert staff of new order
                _staffTMS.TriggerNewOrderAlert();

                if (customer.SMSUpdates)
                {
                    var phone = _smsService.ConvertPhone(customer.Phone);

                    var linkBuilder = new UriBuilder(Request.Scheme, Request.Host.Host, Request.Host.Port ?? -1, $"/Order/Status/{order.Id}");
                    if (linkBuilder.Uri.IsDefaultPort)
                    {
                        linkBuilder.Port = -1;
                    }

                    var link = linkBuilder.Uri.AbsoluteUri;
                    if (_smsService.VerifyPhone(phone))
                    {
                        _smsService.SendSMS(phone, $"Thank you for ordering from the Village Market Deli in Rapid City! Your order total is {order.Total.ToString("C")}. " +
                            $"We will let you know when your order will be ready for pickup or you can check on it yourself at {link}");
                    }
                }
                if (customer.EmailUpdates)
                {
                    var linkBuilder = new UriBuilder(Request.Scheme, Request.Host.Host, Request.Host.Port ?? -1, $"/Order/Status/{order.Id}");
                    if (linkBuilder.Uri.IsDefaultPort)
                    {
                        linkBuilder.Port = -1;
                    }

                    //replace email before launch!
                    var link = linkBuilder.Uri.AbsoluteUri;
                    _emailService.Send(customer.Email, "Thank You For Your Order", $"REPLACE BEFORE LAUNCH: Thank you for ordering from the Village Market Deli in Rapid City! Your order total is {order.Total.ToString("C")}. " +
                            $"We will let you know when your order will be ready for pickup or you can check on it yourself at {link}");
                }

                orderId = order.Id;
            }

            //remove cart from active carts
            _cartService.DesposeCart(cartId);

            return Results.Ok(new
            {
                cartId,
                orderId,
                errorMessage = $"SUCCESS: cart id:{cartId} submitted."
            });
        }

        [HttpPut]
        public IResult ApplyDiscount([FromBody] ApplyCouponModel model)
        {
            _context.MenuItems.Load();

            var cart = _cartService.GetCart(model.CartId);

            if (cart == null)
                return Results.BadRequest("Invalid cart.");

            var coupon = _context.DiscountCodes
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
            _cartService.UpdateCart(cart);

            var discount = coupon.GetDiscount(cart.Order);

            if (discount <= 0)
                return Results.BadRequest(coupon.ErrorReason);

            return Results.Ok($"You saved {discount.ToString("C")}!");
        }

        [HttpPut]
        public IResult Customer([FromBody] CartCustomer model)
        {
            if (!ModelState.IsValid)
            {
                return Results.BadRequest(new
                {
                    model,
                    modelState = ModelState,
                    errorMessage = "Invalid ModelState, please refer to model and modelState for more information."
                });
            }

            var cart = _cartService.GetCart(model.CartId);
            if (cart == null)
            {
                return Results.NotFound(new
                {
                    cartId = model.CartId,
                    errorMessage = "Unable to locate cart! Cart may have expired."
                });
            }

            if (!ModelState.IsValid)
            {
                return Results.BadRequest(new
                {
                    errors = ModelState,
                    errorMessage = "Invalid model state, please correct any errors."
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

            var status = _cartService.UpdateCart(cart);

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

        [HttpPut]
        public IResult UpdateItem([FromBody] CartUpdateItemModel model)
        {
            if (!ModelState.IsValid)
            {
                return Results.BadRequest(new
                {
                    model,
                    modelState = ModelState,
                    errorMessage = "Invalid ModelState, please refer to model and modelState for more information."
                });
            }

            var cart = _cartService.GetCart(model.CartId);
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
                            var ingredient = _context.Ingredients.FirstOrDefault(i => i.Id == id);

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
                var status = _cartService.UpdateCart(cart);

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

        [HttpDelete]
        public IResult Dispose([FromHeader] string cartId)
        {
            var status = _cartService.DesposeCart(cartId);
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
}
