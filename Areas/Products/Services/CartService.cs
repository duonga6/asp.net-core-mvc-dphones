using App.Areas.Products.Models;
using Newtonsoft.Json;

namespace App.Areas.Products.Services {
    public class CartService 
    {
        public const string CARTKEY = "cart";
        public IHttpContextAccessor context;

        public HttpContext httpContext;

        public CartService(IHttpContextAccessor context)
        {
            this.context = context;
            httpContext = context.HttpContext ?? throw new Exception("IHttpContextAccessor not jnjected");
        }

        public List<CartItem>? GetCart()
        {
            var session = httpContext.Session;
            string? jsonCart = session.GetString(CARTKEY);
            if (jsonCart != null)
                return JsonConvert.DeserializeObject<List<CartItem>>(jsonCart);
            return null;
        }

        public void DeleteCart()
        {
            httpContext.Session.Remove(CARTKEY);
        }

        public void SaveCart(List<CartItem>? cartList)
        {
            var session = httpContext.Session;
            var jsonCart = JsonConvert.SerializeObject(cartList);
            session.SetString(CARTKEY, jsonCart);
        }
    }
}