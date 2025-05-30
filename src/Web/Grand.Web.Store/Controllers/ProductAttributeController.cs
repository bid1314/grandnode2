﻿using Grand.Business.Core.Extensions;
using Grand.Business.Core.Interfaces.Catalog.Products;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using Grand.Domain.Permissions;
using Grand.Domain.Seo;
using Grand.Infrastructure;
using Grand.Web.AdminShared.Extensions;
using Grand.Web.AdminShared.Extensions.Mapping;
using Grand.Web.AdminShared.Models.Catalog;
using Grand.Web.Common.DataSource;
using Grand.Web.Common.Filters;
using Grand.Web.Common.Security.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Web.Store.Controllers;

[PermissionAuthorize(PermissionSystemName.ProductAttributes)]
public class ProductAttributeController : BaseStoreController
{
    #region Constructors

    public ProductAttributeController(
        IProductService productService,
        IProductAttributeService productAttributeService,
        ILanguageService languageService,
        ITranslationService translationService,
        IContextAccessor contextAccessor,
        SeoSettings seoSettings)
    {
        _productService = productService;
        _productAttributeService = productAttributeService;
        _languageService = languageService;
        _translationService = translationService;
        _contextAccessor = contextAccessor;
        _seoSettings = seoSettings;
    }

    #endregion

    #region Fields

    private readonly IProductService _productService;
    private readonly IProductAttributeService _productAttributeService;
    private readonly ILanguageService _languageService;
    private readonly ITranslationService _translationService;
    private readonly IContextAccessor _contextAccessor;
    private readonly SeoSettings _seoSettings;

    #endregion Fields

    #region Helper Methods

    /// <summary>
    /// Checks access permissions for a product attribute and handles warnings
    /// </summary>
    /// <param name="productAttribute">Product attribute to check</param>
    /// <returns>True if access is allowed, false if access should be denied</returns>
    private bool CheckAccessPermission(ProductAttribute productAttribute)
    {
        if (productAttribute == null)
            return false;

        if (!productAttribute.LimitedToStores || (productAttribute.LimitedToStores &&
                productAttribute.Stores.Contains(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId) &&
                productAttribute.Stores.Count > 1))
        {
            Warning(_translationService.GetResource("Admin.Catalog.ProductAttribute.Permissions"));
            return true;
        }

        return productAttribute.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId);
    }

    #endregion

    #region Methods

    #region Attribute list / create / edit / delete

    //list
    public IActionResult Index()
    {
        return RedirectToAction("List");
    }

    public IActionResult List()
    {
        return View();
    }

    [PermissionAuthorizeAction(PermissionActionName.List)]
    [HttpPost]
    public async Task<IActionResult> List(DataSourceRequest command)
    {
        var productAttributes = await _productAttributeService
            .GetAllProductAttributes(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId, command.Page - 1, command.PageSize);
        var gridModel = new DataSourceResult {
            Data = productAttributes.Select(x => x.ToModel()),
            Total = productAttributes.TotalCount
        };

        return Json(gridModel);
    }

    //create
    [PermissionAuthorizeAction(PermissionActionName.Create)]
    public async Task<IActionResult> Create()
    {
        var model = new ProductAttributeModel();
        //locales
        await AddLocales(_languageService, model.Locales);
        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Create)]
    [HttpPost]
    [ArgumentNameFilter(KeyName = "save-continue", Argument = "continueEditing")]
    public async Task<IActionResult> Create(ProductAttributeModel model, bool continueEditing)
    {
        if (ModelState.IsValid)
        {
            model.Stores = [_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId];

            var productAttribute = model.ToEntity();
            productAttribute.SeName = SeoExtensions.GetSeName(
                string.IsNullOrEmpty(productAttribute.SeName) ? productAttribute.Name : productAttribute.SeName,
                _seoSettings.ConvertNonWesternChars, _seoSettings.AllowUnicodeCharsInUrls,
                _seoSettings.SeoCharConversion);

            await _productAttributeService.InsertProductAttribute(productAttribute);

            Success(_translationService.GetResource("Admin.Catalog.Attributes.ProductAttributes.Added"));
            return continueEditing
                ? RedirectToAction("Edit", new { id = productAttribute.Id })
                : RedirectToAction("List");
        }

        //If we got this far, something failed, redisplay form
        return View(model);
    }

    //edit
    [PermissionAuthorizeAction(PermissionActionName.Preview)]
    public async Task<IActionResult> Edit(string id)
    {
        var productAttribute = await _productAttributeService.GetProductAttributeById(id);
        if (productAttribute == null)
            //No product attribute found with the specified id
            return RedirectToAction("List");

        if (!CheckAccessPermission(productAttribute))
            return RedirectToAction("List");

        var model = productAttribute.ToModel();
        //locales
        await AddLocales(_languageService, model.Locales, (locale, languageId) =>
        {
            locale.Name = productAttribute.GetTranslation(x => x.Name, languageId, false);
            locale.Description = productAttribute.GetTranslation(x => x.Description, languageId, false);
        });

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    [ArgumentNameFilter(KeyName = "save-continue", Argument = "continueEditing")]
    public async Task<IActionResult> Edit(ProductAttributeModel model, bool continueEditing)
    {
        var productAttribute = await _productAttributeService.GetProductAttributeById(model.Id);
        if (productAttribute == null)
            //No product attribute found with the specified id
            return RedirectToAction("List");

        if (!productAttribute.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
            return RedirectToAction("Edit", new { id = productAttribute.Id });

        if (ModelState.IsValid)
        {
            model.Stores = [_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId];
            productAttribute = model.ToEntity(productAttribute);
            productAttribute.SeName = SeoExtensions.GetSeName(
                string.IsNullOrEmpty(productAttribute.SeName) ? productAttribute.Name : productAttribute.SeName,
                _seoSettings.ConvertNonWesternChars, _seoSettings.AllowUnicodeCharsInUrls,
                _seoSettings.SeoCharConversion);

            await _productAttributeService.UpdateProductAttribute(productAttribute);

            Success(_translationService.GetResource("Admin.Catalog.Attributes.ProductAttributes.Updated"));
            if (continueEditing)
            {
                //selected tab
                await SaveSelectedTabIndex();

                return RedirectToAction("Edit", new { id = productAttribute.Id });
            }

            return RedirectToAction("List");
        }

        //If we got this far, something failed, redisplay form
        // locales
        await AddLocales(_languageService, model.Locales, (locale, languageId) =>
        {
            locale.Name = productAttribute.GetTranslation(x => x.Name, languageId, false);
            locale.Description = productAttribute.GetTranslation(x => x.Description, languageId, false);
        });

        return View(model);
    }

    //delete
    [PermissionAuthorizeAction(PermissionActionName.Delete)]
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var productAttribute = await _productAttributeService.GetProductAttributeById(id);
        if (productAttribute == null)
            //No product attribute found with the specified id
            return RedirectToAction("List");

        if (!productAttribute.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
            return RedirectToAction("List");

        if (ModelState.IsValid)
        {
            await _productAttributeService.DeleteProductAttribute(productAttribute);
            Success(_translationService.GetResource("Admin.Catalog.Attributes.ProductAttributes.Deleted"));
            return RedirectToAction("List");
        }

        Error(ModelState);
        return RedirectToAction("Edit", new { id = productAttribute.Id });
    }

    #endregion

    #region Used by products

    //used by products
    [PermissionAuthorizeAction(PermissionActionName.Preview)]
    [HttpPost]
    public async Task<IActionResult> UsedByProducts(DataSourceRequest command, string productAttributeId)
    {
        var productAttribute = await _productAttributeService.GetProductAttributeById(productAttributeId);

        if (!CheckAccessPermission(productAttribute))
            return RedirectToAction("List");

        var orders = await _productService.GetProductsByProductAttributeId(
            productAttributeId,
            _contextAccessor.WorkContext.CurrentCustomer.StaffStoreId,
            command.Page - 1,
            command.PageSize);

        var gridModel = new DataSourceResult {
            Data = orders.Select(x => new ProductAttributeModel.UsedByProductModel {
                Id = x.Id,
                ProductName = x.Name,
                Published = x.Published
            }),
            Total = orders.TotalCount
        };
        return Json(gridModel);
    }

    #endregion

    #region Predefined values

    [PermissionAuthorizeAction(PermissionActionName.Preview)]
    [HttpPost]
    public async Task<IActionResult> PredefinedProductAttributeValueList(string productAttributeId,
        DataSourceRequest command)
    {
        var productAttribute = await _productAttributeService.GetProductAttributeById(productAttributeId);

        if (!CheckAccessPermission(productAttribute))
            return View("AccessDenied", _translationService.GetResource("Admin.Catalog.ProductAttribute.Permissions"));

        var values = productAttribute.PredefinedProductAttributeValues;
        var gridModel = new DataSourceResult {
            Data = values.Select(x => x.ToModel()),
            Total = values.Count
        };

        return Json(gridModel);
    }

    //create
    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    public async Task<IActionResult> PredefinedProductAttributeValueCreatePopup(string productAttributeId)
    {
        var productAttribute = await _productAttributeService.GetProductAttributeById(productAttributeId);
        if (productAttribute == null)
            throw new ArgumentException("No product attribute found with the specified id");

        if (!productAttribute.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
            return View("AccessDenied", _translationService.GetResource("Admin.Catalog.ProductAttribute.Permissions"));

        var model = new PredefinedProductAttributeValueModel {
            ProductAttributeId = productAttributeId
        };

        //locales
        await AddLocales(_languageService, model.Locales);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> PredefinedProductAttributeValueCreatePopup(
        PredefinedProductAttributeValueModel model)
    {
        var productAttribute = await _productAttributeService.GetProductAttributeById(model.ProductAttributeId);
        if (productAttribute == null)
            throw new ArgumentException("No product attribute found with the specified id");

        if (!productAttribute.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
            return View("AccessDenied", _translationService.GetResource("Admin.Catalog.ProductAttribute.Permissions"));

        if (ModelState.IsValid)
        {
            var ppav = model.ToEntity();
            productAttribute.PredefinedProductAttributeValues.Add(ppav);
            await _productAttributeService.UpdateProductAttribute(productAttribute);
            return Content("");
        }

        //If we got this far, something failed, redisplay form
        return View(model);
    }

    //edit
    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    public async Task<IActionResult> PredefinedProductAttributeValueEditPopup(string id, string productAttributeId)
    {
        var productAttribute = await _productAttributeService.GetProductAttributeById(productAttributeId);

        if (!CheckAccessPermission(productAttribute))
            return View("AccessDenied", _translationService.GetResource("Admin.Catalog.ProductAttribute.Permissions"));

        var ppav = productAttribute.PredefinedProductAttributeValues.FirstOrDefault(x => x.Id == id);
        if (ppav == null)
            throw new ArgumentException("No product attribute value found with the specified id");

        var model = ppav.ToModel();
        //locales
        await AddLocales(_languageService, model.Locales, (locale, languageId) =>
        {
            locale.Name = ppav.GetTranslation(x => x.Name, languageId, false);
        });
        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> PredefinedProductAttributeValueEditPopup(
        PredefinedProductAttributeValueModel model)
    {
        var productAttribute = await _productAttributeService.GetProductAttributeById(model.ProductAttributeId);

        if (!productAttribute.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
            return View("AccessDenied", _translationService.GetResource("Admin.Catalog.ProductAttribute.Permissions"));

        var ppav = productAttribute.PredefinedProductAttributeValues.FirstOrDefault(x => x.Id == model.Id);
        if (ppav == null)
            throw new ArgumentException("No product attribute value found with the specified id");

        if (ModelState.IsValid)
        {
            ppav = model.ToEntity(ppav);
            await _productAttributeService.UpdateProductAttribute(productAttribute);
            return Content("");
        }

        //If we got this far, something failed, redisplay form
        return View(model);
    }

    //delete
    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> PredefinedProductAttributeValueDelete(string id)
    {
        var productAttributes = await _productAttributeService.GetAllProductAttributes(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId);
        var productAttribute = productAttributes.FirstOrDefault(x => x.PredefinedProductAttributeValues.Any(p => p.Id == id));

        if (productAttribute == null)
            throw new ArgumentException("No product attribute found with the specified id");

        if (!productAttribute.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
            return View("AccessDenied", _translationService.GetResource("Admin.Catalog.ProductAttribute.Permissions"));

        if (ModelState.IsValid)
        {
            var ppav = productAttribute.PredefinedProductAttributeValues.FirstOrDefault(x => x.Id == id);
            if (ppav == null)
                throw new ArgumentException("No predefined product attribute value found with the specified id");
            productAttribute.PredefinedProductAttributeValues.Remove(ppav);
            await _productAttributeService.UpdateProductAttribute(productAttribute);
            return new JsonResult("");
        }

        return ErrorForKendoGridJson(ModelState);
    }

    #endregion

    #endregion
}