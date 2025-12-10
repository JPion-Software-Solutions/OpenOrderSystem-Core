using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using OpenOrderSystem.Core.Data.DataModels.Ordering.Entities;
using OpenOrderSystem.Core.Data.DataModels.Ordering.ValueObjects;
using OpenOrderSystem.Core.Models;
using System.Collections.Generic;

namespace OpenOrderSystem.Core.Services
{
    public class CartService
    {
        private Dictionary<string, Cart> _carts = new Dictionary<string, Cart>();
        private List<string> _expiredCarts = new List<string>();

        /// <summary>
        /// Provisions a new cart for use with the CartService
        /// </summary>
        /// <returns>Id of the newly provisioned cart</returns>
        public string ProvisionCart()
        {
            var cart = new Cart();
            _carts[cart.Id] = cart;
            return cart.Id;
        }

        public string ProvisionCartFromExistingOrder(Order order)
        {
            var cart = new Cart();
            cart.Order = order;
            cart.IsExistingOrder = true;
            cart.PromoCode = order.DiscountId;
            cart.Promo = order.Discount;
            _carts[cart.Id] = cart;
            return cart.Id;
        }

        /// <summary>
        /// Retrieves a cart with the provided Id if one exists, otherwise returns null.
        /// </summary>
        /// <param name="id">Id of the cart to locate</param>
        /// <returns>Active cart or null</returns>
        public Cart? GetCart(string id)
        {
            if (_carts.ContainsKey(id))
                return _carts[id];
            else
                return null;
        }

        public void Clean()
        {
            var expired = _carts
                .Where(kvp => kvp.Value.Expired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
                _carts.Remove(key);
        }

        /// <summary>
        /// Updates the information in a cart and resets expiration timer.
        /// </summary>
        /// <param name="updatedCart">updated cart information</param>
        /// <returns>CartStatus describing the action taken</returns>
        public CartStatus UpdateCart(Cart updatedCart)
        {
            Clean();

            var status = CartStatus.NotFound;

            if (_carts.ContainsKey(updatedCart.Id))
            {
                if (updatedCart.Promo != null)
                {
                    var promoAdjustment = updatedCart.Order.PriceAdjustments.FirstOrDefault(a => a.Reason == "Promocode");
                    if (promoAdjustment != null)
                    {
                        promoAdjustment.Amount = updatedCart.Promo.GetDiscount(updatedCart.Order) * -1;
                    }
                    else
                    {
                        updatedCart.Order.PriceAdjustments.Add(new PriceAdjustment
                        {
                            Reason = "Promocode",
                            Amount = updatedCart.Promo.GetDiscount(updatedCart.Order) * -1,
                            Source = "OOSCore.Promotion"
                        });
                    }
                }

                updatedCart.Order.CalculateOrderTotals(new TotalCalculationContext
                {
                    TaxRate = 0.06f
                });

                updatedCart.CartLastActive = DateTime.UtcNow;
                _carts[updatedCart.Id] = updatedCart;
                status = CartStatus.Updated;
            }

            var expired = _expiredCarts
                .AsQueryable()
                .FirstOrDefault(c => c == updatedCart.Id);

            return expired == null ? status : CartStatus.Expired;
        }

        /// <summary>
        /// Dispose a cart with the given Id
        /// </summary>
        /// <param name="cartId">Id of the cart to disposse</param>
        /// <returns>CartStatus describing the action taken</returns>
        public CartStatus DesposeCart(string cartId)
        {
            if (_carts.ContainsKey(cartId))
            {
                _expiredCarts.Add(cartId);
                _carts.Remove(cartId);
                return CartStatus.Disposed;
            }
            var expired = _expiredCarts
                .AsQueryable()
                .FirstOrDefault(c => c == cartId);

            return expired == null ? CartStatus.NotFound : CartStatus.Expired;
        }
    }

    public enum CartStatus
    {
        Active,
        Updated,
        Expired,
        NotFound,
        Disposed
    }
}
