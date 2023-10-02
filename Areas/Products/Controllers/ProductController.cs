using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Transactions;
using App.Areas.Products.Models;
using App.Data;
using App.Models;
using App.Models.Products;
using App.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Areas.Products.Controllers
{
    [Area("Products")]
    [Authorize(Roles = RoleName.Administrator)]
    [Route("/ProductsManager/[action]")]
    public class ProductController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductController> _logger;

        [TempData]
        public string? StatusMessage { set; get; }

        public ProductController(ILogger<ProductController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        private readonly int ITEM_PER_PAGE = 10;

        public IActionResult Index([FromQuery(Name = "p")] int currentPage, [FromQuery(Name = "s")] string? searchString, [FromQuery(Name = "sort")] string? sortString, [FromQuery] string? sortOrder)
        {
            var products = _context.Products.Select(p => p);
            if (searchString != null)
            {
                searchString = searchString.ToLower();
                products = products.Where(p => p.Name.ToLower().Contains(searchString) || (p.Description != null && p.Description.ToLower().Contains(searchString)) || (p.Code != null && p.Code.ToLower().Contains(searchString)));
            }

            if (string.IsNullOrEmpty(sortOrder))
            {
                sortOrder = "desc";
            }

            switch (sortString)
            {
                case "name":
                    products = sortOrder == "asc" ? products.OrderBy(p => p.Name) : products.OrderByDescending(p => p.Name);
                    break;
                case "sold":
                    products = sortOrder == "asc" ? products.OrderBy(p => p.Sold) : products.OrderByDescending(p => p.Sold);
                    break;
                default:
                    products = sortOrder == "asc" ? products.OrderBy(p => p.EntryDate) : products.OrderByDescending(p => p.EntryDate);
                    break;
            }

            ViewBag.OrderSort = sortOrder;

            ViewBag.TotalProduct = products.Count();

            int totalPage = (int)Math.Ceiling((decimal)products.Count() / ITEM_PER_PAGE);
            if (totalPage < 1) totalPage = 1;

            if (currentPage < 1)
                currentPage = 1;

            if (currentPage > totalPage)
                currentPage = totalPage;

            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPage = totalPage;

            var productInPage = products.Skip((currentPage - 1) * ITEM_PER_PAGE).Take(ITEM_PER_PAGE);

            return View(productInPage.ToList());
            // return View(products.ToList());
        }

        [HttpGet]
        public IActionResult Create()
        {
            var category = _context.Categories.ToList();
            var brand = _context.Brands.ToList();
            ViewBag.Category = new MultiSelectList(category, "Id", "Name");
            ViewBag.Brand = new SelectList(brand, "Id", "Name");
            return View();
        }

        [HttpPost, ActionName(nameof(Create))]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAsync(CreateProductModel model)
        {
            var category = _context.Categories.ToList();
            var brand = _context.Brands.ToList();
            ViewBag.Category = new MultiSelectList(category, "Id", "Name");
            ViewBag.Brand = new SelectList(brand, "Id", "Name");

            if (!ModelState.IsValid)
            {
                return View();
            }

            model.Slug ??= AppUtilities.GenerateSlug(model.Name);

            if (_context.Products.Any(p => p.Slug == model.Slug))
            {
                ModelState.AddModelError(string.Empty, "Url nãy đã tồn tại, hãy chọn url khác");
                return View();
            }

            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    var newProduct = new Product()
                    {
                        Name = model.Name,
                        Battery = model.Battery,
                        BrandId = model.BrandId,
                        Camera = model.Camera,
                        Charger = model.Charger,
                        Chipset = model.Chipset,
                        Code = model.Code,
                        Description = model.Description,
                        EntryDate = model.EntryDate,
                        OS = model.OS,
                        Published = model.Published,
                        ReleaseDate = model.ReleaseDate,
                        ScreenSize = model.ScreenSize,
                        SIM = model.SIM,
                        Slug = model.Slug,
                    };

                    await _context.Products.AddAsync(newProduct);
                    await _context.SaveChangesAsync();

                    if (model.CategoryId != null)
                    {
                        foreach (var item in model.CategoryId)
                        {
                            await _context.ProductCategories.AddAsync(new ProductCategory
                            {
                                CategoryId = item,
                                ProductId = newProduct.Id
                            });
                        }
                    }

                    if (model.ProductColor != null)
                    {
                        foreach (var item in model.ProductColor)
                        {
                            var newColor = new Color()
                            {
                                Name = item.Name,
                                Code = item.Code,
                                ProductId = newProduct.Id
                            };

                            if (item.ImageFile != null && item.ImageFile.Length > 0)
                            {
                                var file = item.ImageFile;
                                string currentTime = Regex.Replace(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "[- :.]", "");
                                string filename = currentTime + "-" + newProduct.Slug + Path.GetExtension(file.FileName);
                                string filepath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Products", filename);

                                using var fileStream = new FileStream(filepath, FileMode.Create);
                                await file.CopyToAsync(fileStream);
                                newColor.Image = filename;
                            }

                            await _context.Colors.AddAsync(newColor);
                            await _context.SaveChangesAsync();

                            if (item.Capacities != null)
                            {
                                foreach (var cap in item.Capacities)
                                {
                                    var newCapa = new Capacity()
                                    {
                                        Ram = cap.Ram,
                                        Rom = cap.Rom,
                                        Quantity = cap.Quantity,
                                        ColorId = newColor.Id,
                                        EntryPrice = cap.EntryPrice,
                                        SellPrice = cap.SellPrice
                                    };

                                    await _context.Capacities.AddAsync(newCapa);
                                }
                            }
                        }
                    }

                    if (model.PrimaryImage != null && model.PrimaryImage.Length > 0)
                    {
                        var file = model.PrimaryImage;
                        string currentTime = Regex.Replace(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "[- :.]", "");
                        string filename = currentTime + "-" + newProduct.Slug + Path.GetExtension(file.FileName);
                        string filepath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Products", filename);

                        using var fileStream = new FileStream(filepath, FileMode.Create);
                        await file.CopyToAsync(fileStream);

                        var productPhoto = new ProductPhoto()
                        {
                            Name = filename,
                            ProductId = newProduct.Id
                        };

                        await _context.ProductPhotos.AddAsync(productPhoto);
                        await _context.SaveChangesAsync();
                        newProduct.MainPhoto = productPhoto.Id;
                    }

                    if (model.SubImage != null)
                    {
                        foreach (var item in model.SubImage)
                        {
                            if (item != null && item.FileUpload != null && item.FileUpload.Length > 0)
                            {
                                var file = item.FileUpload;
                                string currentTime = Regex.Replace(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "[- :.]", "");
                                string filename = currentTime + "-" + newProduct.Slug + Path.GetExtension(file.FileName);
                                string filepath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Products", filename);

                                using var fileStream = new FileStream(filepath, FileMode.Create);
                                await file.CopyToAsync(fileStream);

                                _context.ProductPhotos.Add(new ProductPhoto()
                                {
                                    Name = filename,
                                    ProductId = newProduct.Id
                                });
                            }
                        }
                    }
                    await _context.SaveChangesAsync();

                    scope.Complete();
                }
                catch (Exception ex)
                {
                    StatusMessage = "Thêm thất bại. Có lỗi khi thêm: " + ex.ToString();
                    return RedirectToAction(nameof(Index));
                }
            }

            StatusMessage = "Thêm thành công";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("{Id}")]
        public IActionResult Details(int Id)
        {
            var product = _context.Products.Where(p => p.Id == Id)
                                            .Include(p => p.ProductCategories)
                                            .ThenInclude(p => p.Category)
                                            .FirstOrDefault();

            if (product == null) return NotFound();

            return View(product);
        }

        [HttpGet("{Id}")]
        public IActionResult Edit(int Id)
        {
            var product = _context.Products.Where(p => p.Id == Id)
                                            .Include(p => p.Photos)
                                            .Include(p => p.Brand)
                                            .Include(p => p.Colors)
                                            .ThenInclude(c => c.Capacities)
                                            .Include(p => p.ProductCategories)
                                            .ThenInclude(p => p.Category)
                                            .FirstOrDefault();


            if (product == null) return NotFound();

            var photoColor = new List<ColorExtend>();
            product.Colors.ForEach(e => {
                photoColor.Add( new ColorExtend {
                    Id = e.Id,
                    Capacities = e.Capacities,
                    Code = e.Code,
                    Image = e.Image,
                    Name = e.Name,
                });
            });

            CreateProductModel model = new()
            {
                Name = product.Name,
                Battery = product.Battery,
                Camera = product.Camera,
                Charger = product.Charger,
                Chipset = product.Chipset,
                Code = product.Code,
                Description = product.Description,
                EntryDate = product.EntryDate,
                Id = product.Id,
                OS = product.OS,
                Published = product.Published,
                ReleaseDate = product.ReleaseDate,
                ScreenSize = product.ScreenSize,
                SIM = product.SIM,
                Slug = product.Slug,
                Brand = product.Brand,
                BrandId = product.BrandId,
                CategoryId = product.ProductCategories.Select(p => p.CategoryId).ToArray(),
                Colors = product.Colors,
                Photos = product.Photos,
                MainPhoto = product.MainPhoto,
                ProductColor = photoColor
            };

            var category = _context.Categories.ToList();
            ViewBag.Categories = new MultiSelectList(category, "Id", "Name");
            var brand = _context.Brands.ToList();
            ViewBag.Brands = new SelectList(brand, "Id", "Name");

            return View(model);
        }

        public class UploadFile
        {
            public IFormFile? FileUpload { set; get; }
        }

        [HttpPost("{Id:int}"), ActionName(nameof(Edit))]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAsync(int Id, CreateProductModel model, UploadFile newPrimaryPhoto, List<UploadFile> newSubPhoto, List<int> currentSubPhoto, int? currentMainPhoto)
        {
            var productUpdate = _context.Products.Where(p => p.Id == Id)
                                            .Include(p => p.Photos)
                                            .Include(p => p.Brand)
                                            .Include(p => p.Colors)
                                            .ThenInclude(c => c.Capacities)
                                            .Include(p => p.ProductCategories)
                                            .ThenInclude(p => p.Category)
                                            .FirstOrDefault();

            if (productUpdate == null) return NotFound();
            
            var category = _context.Categories.ToList();
            ViewBag.Categories = new MultiSelectList(category, "Id", "Name");
            var brand = _context.Brands.ToList();
            ViewBag.Brands = new SelectList(brand, "Id", "Name");

            model.Photos = productUpdate.Photos;

            if (!ModelState.IsValid) return View(model);

            model.Slug ??= AppUtilities.GenerateSlug(model.Name);

            if (_context.Products.FirstOrDefault(p => p.Slug == model.Slug && p.Id != Id) != null)
            {
                ModelState.AddModelError(string.Empty, "Url này đã sử dụng, hãy dùng url khác");
                return View(model);
            }

            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    // Cập nhật thông tin cơ bản
                    productUpdate.Code = model.Code;
                    productUpdate.Name = model.Name;
                    productUpdate.BrandId = model.BrandId;
                    productUpdate.Slug = model.Slug;
                    productUpdate.Battery = model.Battery;
                    productUpdate.OS = model.OS;
                    productUpdate.Chipset = model.Chipset;
                    productUpdate.ScreenSize = model.ScreenSize;
                    productUpdate.Charger = model.Charger;
                    productUpdate.EntryDate = model.EntryDate;
                    productUpdate.ReleaseDate = model.ReleaseDate;
                    productUpdate.SIM = model.SIM;
                    productUpdate.Camera = model.Camera;
                    productUpdate.Description = model.Description;
                    productUpdate.Published = model.Published;

                    // Xóa ảnh chính
                    if (currentMainPhoto == null)
                    {
                        var oldPhoto = productUpdate.Photos?.Where(p => p.Id == productUpdate.MainPhoto).FirstOrDefault();
                        if (oldPhoto != null)
                        {
                            productUpdate.MainPhoto = null;
                            DeleteImgFile(oldPhoto.Name);
                        }
                    }

                    // Thêm ảnh chính
                    if (newPrimaryPhoto != null && newPrimaryPhoto.FileUpload != null && newPrimaryPhoto.FileUpload.Length > 0)
                    {
                        var file = newPrimaryPhoto.FileUpload;
                        string currentTime = Regex.Replace(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "[- :.]", "");
                        string filename = currentTime + "_" + productUpdate.Slug + Path.GetExtension(file.FileName);
                        string filepath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Products", filename);

                        using var fileStream = new FileStream(filepath, FileMode.Create);
                        await file.CopyToAsync(fileStream);


                        var newPhoto = new ProductPhoto()
                        {
                            Name = filename,
                            ProductId = productUpdate.Id
                        };

                        await _context.ProductPhotos.AddAsync(newPhoto);
                        await _context.SaveChangesAsync();

                        productUpdate.MainPhoto = newPhoto.Id;
                    }

                    // Xóa ảnh phụ
                    var removePhoto = productUpdate.Photos?.Where(p => p.Id != productUpdate.MainPhoto && (currentSubPhoto == null || !currentSubPhoto.Contains(p.Id))).ToList();
                    if (removePhoto != null)
                    {
                        _context.ProductPhotos.RemoveRange(removePhoto);
                        removePhoto.ForEach(item => {
                            DeleteImgFile(item.Name);
                        });
                    }
                    // Thêm ảnh phụ
                    if (newSubPhoto != null)
                    {
                        foreach (var item in newSubPhoto)
                        {
                            if (item != null && item.FileUpload != null && item.FileUpload.Length > 0)
                            {
                                var file = item.FileUpload;
                                string currentTime = Regex.Replace(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), "[- :.]", "");
                                string filename = currentTime + "_" + productUpdate.Slug + Path.GetExtension(file.FileName);
                                string filepath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Products", filename);

                                using var fileStream = new FileStream(filepath, FileMode.Create);
                                await file.CopyToAsync(fileStream);

                                _context.ProductPhotos.Add(new ProductPhoto
                                {
                                    Name = filename,
                                    ProductId = productUpdate.Id
                                });
                            }
                        }
                    }

                    // Cập nhật danh mục

                    var oldCate = productUpdate.ProductCategories.ToList();
                    var newCate = model.CategoryId;

                    var addCate = newCate?.Where(c => !oldCate.Where(ca => ca.CategoryId == c).Any());
                    var removeCate = oldCate.Where(c => newCate != null && !newCate.Contains(c.CategoryId));

                    if (removeCate != null)
                        _context.ProductCategories.RemoveRange(removeCate);
                    
                    if (addCate != null)
                    {
                        foreach (var item in addCate)
                        {
                            _context.ProductCategories.Add(new ProductCategory{
                                CategoryId = item,
                                Product = productUpdate
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    scope.Complete();
                }
                catch (Exception ex)
                {
                    StatusMessage = "Có lỗi khi thêm: " + ex.ToString();
                    return RedirectToAction(nameof(Index));
                }
            }

            StatusMessage = "Cập nhật thành công";
            return RedirectToAction(nameof(Index));


            // return Json(new
            // {
            //     model,
            //     newPrimaryPhoto,
            //     newSubPhoto,
            //     currentSubPhoto
            // });
        }

        [HttpGet("{Id}")]
        public IActionResult Delete(int Id)
        {
            var product = _context.Products.Where(p => p.Id == Id).FirstOrDefault();
            if (product == null) return NotFound();

            return View(product);
        }

        [HttpPost("{Id}"), ActionName(nameof(Delete))]
        public async Task<IActionResult> DeleteAsync(int Id)
        {
            var product = await _context.Products.Where(p => p.Id == Id)
                                            .Include(p => p.ProductCategories)
                                            .Include(p => p.Photos)
                                            .Include(p => p.Colors)
                                            .ThenInclude(c => c.Capacities)
                                            .FirstOrDefaultAsync();

            if (product == null) return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            List<string> filenames = new();

            product.Photos?.ForEach(p => filenames.Add(p.Name));

            product.Colors?.ForEach(c =>
            {
                if (c != null && c.Image != null)
                    filenames.Add(c.Image);
            });

            filenames.ForEach(f =>
            {
                if (f != null)
                {
                    DeleteImgFile(f);
                }
            });



            StatusMessage = "Xóa thành công";
            return RedirectToAction(nameof(Index));
        }

        private void DeleteImgFile(string filename)
        {
            string filepath = Path.Combine("Uploads", "Products", filename);
            if (System.IO.File.Exists(filepath))
            {
                System.IO.File.Delete(filepath);
            }
        }

        [HttpGet]
        public IActionResult AddPhoto(int Id)
        {
            var product = _context.Products.Where(p => p.Id == Id).FirstOrDefault();
            if (product == null)
            {
                StatusMessage = "Có lỗi khi thêm sản phẩm";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Product = product;

            return View();
        }

        public class UploadOneFile
        {
            [Display(Name = "Chọn hình ảnh")]
            [DataType(DataType.Upload)]
            [FileExtensions(Extensions = "png, jpg, jpeg, webp")]
            public IFormFile? FileUpload { set; get; }
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult GetPhoto(int Id)
        {
            var product = _context.Products.Where(p => p.Id == Id).FirstOrDefault();
            if (product == null) return NotFound();

            var mainPhoto = _context.ProductPhotos.Where(p => p.Id == product.MainPhoto).Select(p => new
            {
                id = p.Id,
                filename = p.Name
            }).FirstOrDefault();

            var subPhoto = _context.ProductPhotos.Where(p => p.ProductId == product.Id && p.Id != product.MainPhoto).Select(p => new
            {
                id = p.Id,
                filename = p.Name
            }).ToList();

            return Json(new
            {
                mainPhoto,
                subPhoto
            });
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(int Id, [Bind("FileUpload")] UploadOneFile f, string type)
        {
            var product = _context.Products.Where(p => p.Id == Id).FirstOrDefault();
            if (product == null) return NotFound("Không tìm thấy sản phẩm này");

            var file = f.FileUpload;

            if (file != null && file.Length > 0)
            {
                var filename = Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + Path.GetExtension(file.FileName);
                var filePath = Path.Combine("Uploads", "Products", filename);

                using var fileStream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(fileStream);

                var photo = new ProductPhoto()
                {
                    Name = filename,
                    Product = product
                };

                _context.ProductPhotos.Add(photo);
                _context.SaveChanges();

                if (type == "main") product.MainPhoto = photo.Id;

                _context.SaveChanges();
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int Id, int productid, string type)
        {
            var product = _context.Products.Where(p => p.Id == productid).FirstOrDefault();
            if (product == null) return NotFound();

            var photo = _context.ProductPhotos.Where(p => p.Id == Id).FirstOrDefault();
            if (photo == null) return NotFound();

            string filepath = Path.Combine("Uploads", "Products", photo.Name);
            if (System.IO.File.Exists(filepath))
            {
                System.IO.File.Delete(filepath);
            }

            if (type == "main") product.MainPhoto = null;
            _context.ProductPhotos.Remove(photo);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}