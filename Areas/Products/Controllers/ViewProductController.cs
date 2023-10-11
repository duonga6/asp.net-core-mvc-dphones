using App.Areas.Products.Models;
using App.Areas.Products.Services;
using App.Data;
using App.Models;
using App.Models.Products;
using App.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq;

namespace App.Areas.Products.Controllers
{
    [Area("Products")]
    public class ViewProductController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductController> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly CartService _cart;
        private readonly IEmailSender _emailSender;

        [TempData]
        public string? StatusMessage { set; get; }

        public ViewProductController(ILogger<ProductController> logger, AppDbContext context, CartService cart, UserManager<AppUser> userManager, IEmailSender emailSender)
        {
            _logger = logger;
            _context = context;
            _cart = cart;
            _userManager = userManager;
            _emailSender = emailSender;
        }


        [Route("/{slug}")]
        public IActionResult Details(string slug)
        {
            var product = _context.Products.Where(p => p.Slug == slug)
                                            .Include(p => p.Photos)
                                            .Include(p => p.Colors)
                                            .ThenInclude(c => c.Capacities)
                                            .AsSplitQuery()
                                            .FirstOrDefault();

            if (product == null) return NotFound();

            product.Colors = product.Colors.OrderBy(c => c.Name).ToList();
            product.Colors.ForEach(c =>
            {
                c.Capacities = c.Capacities.OrderBy(ca => ca.Rom).ToList();
            });

            return View(product);
        }

        [HttpGet]
        [Route("/get-color")]
        public IActionResult GetColor(int productId)
        {
            var color = _context.Colors.Where(c => c.ProductId == productId).OrderBy(c => c.Name).ToList();

            return Ok(JsonConvert.SerializeObject(color));
        }

        [HttpGet]
        [Route("/get-capacity")]
        public IActionResult GetCapacity(int colorId)
        {
            var capacity = _context.Capacities.Where(c => c.ColorId == colorId).OrderBy(c => c.Rom).ToList();
            return Ok(JsonConvert.SerializeObject(capacity));
        }


        [Route("/cart")]
        public IActionResult Cart()
        {
            return View();
        }

        [HttpPost]
        [Route("/cart/add-to-cart")]
        public IActionResult AddToCart(int productId, int colorId, int capaId)
        {
            var product = _context.Products.AsNoTracking().FirstOrDefault(p => p.Id == productId);
            var color = _context.Colors.AsNoTracking().FirstOrDefault(c => c.Id == colorId && c.ProductId == productId);
            var capa = _context.Capacities.AsNoTracking().FirstOrDefault(c => c.Id == capaId && c.ColorId == colorId);

            if (product == null || color == null || capa == null)
            {
                return Json(new
                {
                    status = 0,
                    message = "Thêm thất bại. Không tìm thấy sản phẩm yêu cầu!"
                });
            }

            product.Description = null;

            var cartItem = new CartItem()
            {
                Id = Guid.NewGuid().ToString(),
                Product = product,
                Capacity = capa,
                Color = color,
                Quantity = 1
            };

            var cartList = _cart.GetCart();
            cartList ??= new List<CartItem>();
            var productInCart = cartList.FirstOrDefault(p => p.Product.Id == productId && p.Color.Id == colorId && p.Capacity.Id == capaId);
            if (productInCart == null)
            {
                cartList.Add(cartItem);
            }
            else
            {
                if (productInCart.Quantity >= productInCart.Capacity.Quantity)
                {
                    return Json(new
                    {
                        status = 0,
                        message = "Sản phẩm bạn chọn đã hết!"
                    });
                }
                else
                {
                    productInCart.Quantity++;
                }
            }

            _cart.SaveCart(cartList);

            return Json(new
            {
                status = 1,
                message = "Đã thêm vào giỏ hàng!",
                qtt = cartList.Count()
            });
        }

        [HttpPut]
        [Route("/cart/plus")]
        public IActionResult PlusQuantity(string Id)
        {
            var cartList = _cart.GetCart();
            var cartItem = cartList?.FirstOrDefault(c => c.Id == Id);
            if (cartItem != null)
            {
                if (cartItem.Quantity >= cartItem.Capacity.Quantity)
                {
                    return Ok(new
                    {
                        status = 0,
                        message = "Sản phẩm này đã hết!",
                        qtt = cartItem.Quantity
                    });
                }
                else
                {
                    cartItem.Quantity++;
                    _cart.SaveCart(cartList);
                    return Ok(new
                    {
                        status = 1,
                        message = "Đã tăng số lượng!",
                        qtt = cartItem.Quantity
                    });
                }
            }
            else
            {
                return NotFound(new
                {
                    status = 0,
                    message = "Không tìm thấy sản phẩm này!"
                });
            }
        }

        [HttpPut]
        [Route("/cart/minus")]
        public IActionResult MinusQuantity(string Id)
        {
            var cartList = _cart.GetCart();
            var cartItem = cartList?.FirstOrDefault(c => c.Id == Id);
            if (cartItem != null)
            {
                if (cartItem.Quantity <= 1)
                {
                    return Ok(new
                    {
                        status = 0,
                        message = "Số lượng phải >= 1",
                        qtt = cartItem.Quantity
                    });
                }
                else
                {
                    cartItem.Quantity--;
                    _cart.SaveCart(cartList);
                    return Ok(new
                    {
                        status = 1,
                        message = "Đã giảm số lượng",
                        qtt = cartItem.Quantity
                    });
                }
            }
            else
            {
                return NotFound(new
                {
                    status = 0,
                    message = "Không tìm thấy sản phẩm này"
                });
            }
        }

        [HttpDelete]
        [Route("/cart/delete-item")]
        public IActionResult DeleteCartItem(string Id)
        {
            var cartList = _cart.GetCart();
            var cartItem = cartList?.FirstOrDefault(c => c.Id == Id);

            if (cartItem == null)
                return Json(new
                {
                    status = 0,
                    message = "Lỗi. Không tìm thấy sản phẩm này trong giỏ hàng!"
                });
            else
                cartList?.Remove(cartItem);

            _cart.SaveCart(cartList);

            return Ok(new
            {
                status = 1,
                message = "Đã xóa sản phẩm!",
                qtt = cartList?.Count()
            });
        }

        [HttpGet]
        [Route("/cart/get-cart")]
        public IActionResult GetCartList()
        {
            var cartList = _cart.GetCart();
            return Ok(JsonConvert.SerializeObject(cartList));
        }

        [HttpGet]
        [Route("/order")]
        public IActionResult CreateOrder(string[] cartId)
        {
            var cartList = _cart.GetCart();
            var cartItemSelected = cartList?.Where(c => cartId.Contains(c.Id)).ToList();


            ViewBag.CartList = cartItemSelected;
            return View();
        }

        [HttpPost, ActionName(nameof(CreateOrder))]
        [ValidateAntiForgeryToken]
        [Route("/order")]
        public async Task<IActionResult> CreateOrderAsync(Order model, string[] cartId)
        {
            var cartList = _cart.GetCart();
            var cartItemSelected = cartList?.Where(c => cartId.Contains(c.Id)).ToList();
            ViewBag.CartList = cartItemSelected;

            if (cartItemSelected == null)
            {
                StatusMessage = "Có lỗi xảy ra, không tìm thấy sản phẩm";
                return View(model);
            }

            var totalCost = cartItemSelected.Sum(c => c.Capacity.SellPrice * c.Quantity);

            var orderDetailList = new List<OrderDetail>();

            cartItemSelected?.ForEach(item =>
            {
                orderDetailList.Add(new OrderDetail()
                {
                    ProductId = item.Product.Id,
                    ColorId = item.Color.Id,
                    CapacityId = item.Capacity.Id,
                    Quantity = item.Quantity,
                    SellPrice = item.Capacity.SellPrice
                });
            });

            var orderStatus = new OrderStatus() {
                Code = 0,
                Status = OrderStatuses.WaitAccept,
                DateUpdate = DateTime.Now,
                Note = "Khách hàng đặt hàng trên trang web",
            };

            var order = new Order()
            {
                FullName = model.FullName,
                City = model.City,
                Commune = model.Commune,
                District = model.District,
                Email = model.Email,
                PayType = model.PayType,
                PhoneNumber = model.PhoneNumber,
                OrderDate = DateTime.Now,
                SpecificAddress = model.SpecificAddress,
                Code = DateTime.Now.ToString("yyyyMMddHH") + "-" + model.PhoneNumber,
                TotalCost = totalCost,
                OrderDetails = orderDetailList,
                OrderStatuses = new List<OrderStatus>() {
                    orderStatus
                }
            };

            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                order.UserId = user.Id;
                orderStatus.UserId = user.Id;
            }


            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();

            cartItemSelected?.ForEach(item =>
            {
                cartList?.Remove(item);
            });

            _cart.SaveCart(cartList);

            string orderMessage = user == null ? "Chúng tôi sẽ liên lạc với bạn để xác nhận đơn hàng này. Nếu không xác nhận được đơn hàng sẽ bị hủy." : "Đơn hàng của bạn đang được chúng tôi xử lý.";

            string emailContent = 
$@"
Cảm ơn bạn đã đặt hàng. {orderMessage}
Vui lòng theo dõi đơn hàng trong mục Theo dõi đơn hàng.

Mã đơn hàng: {order.Code}

Mọi thông tin chi tiết vui lòng liên hệ 1800.1789
Xin cảm ơn.";

            string emailHtml = AppUtilities.GenerateHtmlEmail(order.FullName, emailContent);

            await _emailSender.SendEmailAsync(order.Email, "Đặt hàng thành công", emailHtml);

            return View(nameof(OrderConfirmed));
        }

        [Route("/order-confirmed")]
        public IActionResult OrderConfirmed()
        {
            return View();
        }

        [HttpGet]
        [Route("/order-check")]
        public IActionResult OrderCheck()
        {
            return View();
        }

        [HttpPost, ActionName(nameof(OrderCheck))]
        [Route("/order-check")]
        public IActionResult OrderCheckAsync(string PhoneNumber, string Code)
        {
            var order = _context.Orders.Where(o => o.PhoneNumber == PhoneNumber && o.Code == Code)
                                        .Include(o => o.OrderDetails)
                                        .Include(o => o.OrderStatuses)
                                        .FirstOrDefault();

            order?.OrderDetails.ForEach(o => {
                o.Product = _context.Products.FirstOrDefault(p => p.Id == o.ProductId);
                o.Color = _context.Colors.FirstOrDefault(c => c.Id == o.ColorId);
                o.Capacity = _context.Capacities.FirstOrDefault(c => c.Id == o.CapacityId);
            });

            if (order?.OrderStatuses != null)
            {
                order.OrderStatuses = order.OrderStatuses.OrderBy(s => s.DateUpdate).ToList();
            }
            return View(order);
        }
    }
}