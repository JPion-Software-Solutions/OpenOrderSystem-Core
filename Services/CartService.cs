using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Models;
using System.Collections.Generic;

namespace OpenOrderSystem.Core.Services
{
    public class CartService
    {
        private Dictionary<string, LegacyCart> _carts = new Dictionary<string, LegacyCart>();
        private List<string> _expiredCarts = new List<string>();

        /// <summary>
        /// Provisions a new cart for use with the CartService
        /// </summary>
        /// <returns>Id of the newly provisioned cart</returns>
        public string ProvisionCart()
        {
            var cart = new LegacyCart();
            _carts[cart.Id] = cart;
            return cart.Id;
        }

        public string ProvisionCartFromExistingOrder(Order order)
        {
            var cart = new LegacyCart();
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
        public LegacyCart? GetCart(string id)
        {
            if (_carts.ContainsKey(id))
                return _carts[id];
            else
                return null;
        }

        public void Clean()
        {
            foreach (var id in _carts.Keys)
            {
                if (_carts[id].Expired)
                {
                    _carts.Remove(id);
                }
            }
        }

        /// <summary>
        /// Updates the information in a cart and resets expiration timer.
        /// </summary>
        /// <param name="updatedLegacyCart">updated cart information</param>
        /// <returns>CartStatus describing the action taken</returns>
        public CartStatus UpdateCart(LegacyCart updatedLegacyCart)
        {
            Clean();

            var status = CartStatus.NotFound;

            if (_carts.ContainsKey(updatedLegacyCart.Id))
            {
                updatedLegacyCart.CartLastActive = DateTime.UtcNow;
                _carts[updatedLegacyCart.Id] = updatedLegacyCart;
                status = CartStatus.Updated;
            }

            var expired = _expiredCarts
                .AsQueryable()
                .FirstOrDefault(c => c == updatedLegacyCart.Id);

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
