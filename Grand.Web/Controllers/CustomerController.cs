using Amazon.Runtime;
using Grand.Api.Commands.Models.Customers;
using Grand.Api.DTOs.Customers;
using Grand.Api.Extensions;
using Grand.Core;
using Grand.Core.Domain;
using Grand.Core.Domain.Customers;
using Grand.Framework.Controllers;
using Grand.Framework.Mvc.Filters;
using Grand.Framework.Security.Captcha;
using Grand.Services.Authentication;
using Grand.Services.Authentication.External;
using Grand.Services.Common;
using Grand.Services.Customers;
using Grand.Services.Directory;
using Grand.Services.ExportImport;
using Grand.Services.Localization;
using Grand.Services.Logging;
using Grand.Services.Messages;
using Grand.Services.Orders;
using Grand.Services.Security;
using Grand.Web.Commands.Models.Customers;
using Grand.Web.Extensions;
using Grand.Web.Features.Models.Common;
using Grand.Web.Features.Models.Customers;
using Grand.Web.Models.Customer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Controllers
{
    public partial class CustomerController : BasePublicController
    {
        #region Fields
        private readonly IEncryptionService _encryptionService;
        private readonly IGrandAuthenticationService _authenticationService;
        private readonly ILocalizationService _localizationService;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ICustomerRegistrationService _customerRegistrationService;
        private readonly ICountryService _countryService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly IMediator _mediator;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly CustomerSettings _customerSettings;
        private readonly CaptchaSettings _captchaSettings;

        #endregion

        #region Ctor

        public CustomerController(
            IGrandAuthenticationService authenticationService,
            ILocalizationService localizationService,
            IWorkContext workContext,
            IStoreContext storeContext,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            ICustomerRegistrationService customerRegistrationService,
            ICountryService countryService,
            ICustomerActivityService customerActivityService,
            IMediator mediator,
            IWorkflowMessageService workflowMessageService,
            CaptchaSettings captchaSettings,
            CustomerSettings customerSettings,
            IEncryptionService encryptionService)
        {
            _authenticationService = authenticationService;
            _localizationService = localizationService;
            _workContext = workContext;
            _storeContext = storeContext;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _customerRegistrationService = customerRegistrationService;
            _customerSettings = customerSettings;
            _countryService = countryService;
            _customerActivityService = customerActivityService;
            _workflowMessageService = workflowMessageService;
            _captchaSettings = captchaSettings;
            _mediator = mediator;
            _encryptionService = encryptionService;
        }

        #endregion

        #region Login / logout

        //available even when a store is closed
        [CheckAccessClosedStore(true)]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual IActionResult Login(bool? checkoutAsGuest)
        {
            var model = new LoginModel();
            model.UsernamesEnabled = _customerSettings.UsernamesEnabled;
            model.CheckoutAsGuest = checkoutAsGuest.GetValueOrDefault();
            model.DisplayCaptcha = _captchaSettings.Enabled && _captchaSettings.ShowOnLoginPage;
            return View(model);
        }

        [HttpPost]
        //available even when a store is closed
        [CheckAccessClosedStore(true)]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        [ValidateCaptcha]
        [AutoValidateAntiforgeryToken]
        public virtual async Task<IActionResult> Login(LoginModel model, string returnUrl, bool captchaValid,
                       [FromServices] IShoppingCartService shoppingCartService)
        {
            //validate CAPTCHA
            if (_captchaSettings.Enabled && _captchaSettings.ShowOnLoginPage && !captchaValid)
            {
                ModelState.AddModelError("", _captchaSettings.GetWrongCaptchaMessage(_localizationService));
            }

            if (ModelState.IsValid)
            {
                if (_customerSettings.UsernamesEnabled && model.Username != null)
                {
                    model.Username = model.Username.Trim();
                }
                var loginResult = await _customerRegistrationService.ValidateCustomer(_customerSettings.UsernamesEnabled ? model.Username : model.Email, model.Password);
                switch (loginResult)
                {
                    case CustomerLoginResults.Successful:
                        {
                            var customer = _customerSettings.UsernamesEnabled ? await _customerService.GetCustomerByUsername(model.Username) : await _customerService.GetCustomerByEmail(model.Email);
                            //sign in
                            return await SignInAction(shoppingCartService, customer, returnUrl);
                        }
                    case CustomerLoginResults.RequiresTwoFactor:
                        {
                            var userName = _customerSettings.UsernamesEnabled ? model.Username : model.Email;

                            HttpContext.Session.SetString("RequiresTwoFactor", userName);

                            return RedirectToRoute("TwoFactorAuthorization");
                        }

                    case CustomerLoginResults.CustomerNotExist:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.CustomerNotExist"));
                        break;
                    case CustomerLoginResults.Deleted:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.Deleted"));
                        break;
                    case CustomerLoginResults.NotActive:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.NotActive"));
                        break;
                    case CustomerLoginResults.NotRegistered:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.NotRegistered"));
                        break;
                    case CustomerLoginResults.LockedOut:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.LockedOut"));
                        break;
                    case CustomerLoginResults.WrongPassword:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials"));
                        break;
                    default:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials"));
                        break;
                }
            }

            //If we got this far, something failed, redisplay form
            model.UsernamesEnabled = _customerSettings.UsernamesEnabled;
            model.DisplayCaptcha = _captchaSettings.Enabled && _captchaSettings.ShowOnLoginPage;

            return View(model);
        }

        public async Task<IActionResult> TwoFactorAuthorization()
        {
            if (!_customerSettings.TwoFactorAuthenticationEnabled)
                return RedirectToRoute("Login");

            var username = HttpContext.Session.GetString("RequiresTwoFactor");
            if (string.IsNullOrEmpty(username))
                return RedirectToRoute("HomePage");

            var customer = _customerSettings.UsernamesEnabled ? await _customerService.GetCustomerByUsername(username) : await _customerService.GetCustomerByEmail(username);
            if (customer == null)
                return RedirectToRoute("HomePage");

            if (!customer.GetAttributeFromEntity<bool>(SystemCustomerAttributeNames.TwoFactorEnabled))
                return RedirectToRoute("HomePage");

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> LoginRedirectToPlatform(string reference)
        {
            var customer = await _customerService.GetCustomerById(reference);
            if (customer != null)
            {
                //sign in new customer
                await _authenticationService.SignIn(customer, true);
                //on success redirect to platform
                return RedirectPermanent(_localizationService.GetResource("Platform.BaseUrl"));
            }
            return RedirectPermanent(_localizationService.GetResource("Platform.LoginUrl"));
        }

        [HttpPost]
        public async Task<IActionResult> TwoFactorAuthorization(string token,
            [FromServices] IShoppingCartService shoppingCartService,
            [FromServices] ITwoFactorAuthenticationService twoFactorAuthenticationService
            )
        {
            if (!_customerSettings.TwoFactorAuthenticationEnabled)
                return RedirectToRoute("Login");

            var username = HttpContext.Session.GetString("RequiresTwoFactor");
            if (string.IsNullOrEmpty(username))
                return RedirectToRoute("HomePage");

            var customer = _customerSettings.UsernamesEnabled ? await _customerService.GetCustomerByUsername(username) : await _customerService.GetCustomerByEmail(username);
            if (customer == null)
                return RedirectToRoute("Login");

            if (string.IsNullOrEmpty(token))
            {
                ModelState.AddModelError("", _localizationService.GetResource("Account.TwoFactorAuth.SecurityCodeIsRequired"));
            }
            else
            {
                var secretKey = customer.GetAttributeFromEntity<string>(SystemCustomerAttributeNames.TwoFactorSecretKey);
                if (twoFactorAuthenticationService.AuthenticateTwoFactor(secretKey, token))
                {
                    //remove session
                    HttpContext.Session.Remove("RequiresTwoFactor");

                    //sign in
                    return await SignInAction(shoppingCartService, customer);
                }
                ModelState.AddModelError("", _localizationService.GetResource("Account.TwoFactorAuth.WrongSecurityCode"));
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public virtual async Task<IActionResult> RegisterExternal([FromBody] GenericModel.RegistrationModel model)
        {
            GenericModel.Result result = new GenericModel.Result();
            try
            {
                CustomerDto customerDto = new CustomerDto()
                {
                    Username = model.email,
                    Email = model.email,
                    AdminComment = "Generated from External",
                    IsTaxExempt = true,
                    FreeShipping = true,
                    AffiliateId = "",
                    VendorId = "",
                    StoreId = _storeContext.CurrentStore.Id.ToString(),
                    Active = true,
                    Deleted = false,
                    Gender = model.gender,
                    FirstName = model.firstName,
                    LastName = model.lastName,
                    DateOfBirth = DateTime.Now,
                    Company = "-",
                    StreetAddress = "-",
                    StreetAddress2 = "-",
                    ZipPostalCode = "0",
                    City = "-",
                    CountryId = "",
                    StateProvinceId = "",
                    Phone = "-",
                    VatNumber = "",
                    VatNumberStatusId = "",
                    Signature = ""
                };



                var registeredRole = await _customerService.GetCustomerRoleBySystemName(SystemCustomerRoleNames.Registered);
                var newUserRole = await _customerService.GetCustomerRoleBySystemName("New User");
                var customer = customerDto.ToEntity();
                customer.CreatedOnUtc = DateTime.UtcNow;
                customer.LastActivityDateUtc = DateTime.UtcNow;
                customer.CustomerRoles.Add(registeredRole);
                customer.CustomerRoles.Add(newUserRole);
                if (!String.IsNullOrEmpty(model.gender))
                {
                    var femaleGenderRole = await _customerService.GetCustomerRoleBySystemName("Women");
                    if (femaleGenderRole != null)
                    {
                        customer.CustomerRoles.Add(femaleGenderRole);
                    }
                }


                var country = await _countryService.GetCountryByTwoLetterIsoCode("PH");

                string saltKey = _encryptionService.CreateSaltKey(5);
                customer.PasswordFormat = PasswordFormat.Hashed;
                customer.PasswordSalt = saltKey;
                customer.Password = _encryptionService.CreatePasswordHash(customer.Password, saltKey, _customerSettings.HashedPasswordFormat);

                AddCustomerAddressCommand addressCommand = new AddCustomerAddressCommand();
                addressCommand.Address = new AddressDto();
                addressCommand.Address.Address1 = "";
                addressCommand.Address.Address2 = "";
                addressCommand.Address.VatNumber = "";
                addressCommand.Address.PhoneNumber = "";
                addressCommand.Address.City = "";
                addressCommand.Address.Company = "";
                addressCommand.Address.CreatedOnUtc = DateTime.Now;
                addressCommand.Address.Email = model.email;
                addressCommand.Address.FaxNumber = "";
                addressCommand.Address.ZipPostalCode = "";
                addressCommand.Address.FirstName = model.firstName;
                addressCommand.Address.LastName = model.lastName;
                addressCommand.Address.City = "";
                addressCommand.Address.CustomAttributes = "";
                addressCommand.Address.CountryId = country.Id;
                addressCommand.Address.StateProvinceId = "";

                if (await _customerService.GetCustomerByEmail(customer.Email) != null)
                {
                    result = new GenericModel.Result { Message = _localizationService.GetResource("Account.Register.Errors.EmailAlreadyExists"), StatusCode = 500, IsSuccess = false };
                }
                else
                {
                    await _customerService.InsertCustomer(customer);
                    var address = addressCommand.Address.ToEntity();
                    address.CustomerId = customer.Id;
                    await _customerService.InsertAddress(address);
                    result = new GenericModel.Result { Message = "User created", StatusCode = 200, IsSuccess = true, ResponseObject = customer.Id.ToString() };
                }
            }
            catch (Exception ex)
            {
                result = new GenericModel.Result { Message = ex.ToString(), StatusCode = 500, IsSuccess = false };
            }
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost]
        [AllowAnonymous]
        public virtual async Task<IActionResult> UpdateCustomerProfile([FromBody]GenericModel.UpdateCustomerProfile model)
        {
            GenericModel.Result result = new GenericModel.Result();
            try
            {
                var customer = await _customerService.GetCustomerById(model.reference);
                if (customer != null)
                {
                    var femaleRole = await _customerService.GetCustomerRoleBySystemName("Women");
                    //Update Gender role 
                    if (model.gender.ToUpper() == "FEMALE")
                    {
                        if (!customer.CustomerRoles.Where(w => w.SystemName.ToUpper() == "WOMEN").Any())
                        {
                            femaleRole.CustomerId = customer.Id;
                            customer.CustomerRoles.Add(femaleRole);
                            await _customerService.InsertCustomerRoleInCustomer(femaleRole);
                        }
                    }
                    else
                    {
                        await _customerService.RemoveRoleFromCustomer(customer, "Women");
                        customer.CustomerRoles.Remove(femaleRole);
                    }
                    await _customerService.UpdateCustomer(customer);
                    result = new GenericModel.Result { IsSuccess = true, Message = "Customer updated", StatusCode = 200 };
                }
                else
                {
                    result = new GenericModel.Result { IsSuccess = false, Message = "Customer does not exist", StatusCode = 500, ResponseObject = model };
                }
            }
            catch (Exception ex)
            {
                result = new GenericModel.Result { IsSuccess = false, Message = ex.ToString(), StatusCode = 500 };
            }
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost]
        [AllowAnonymous]
        public virtual async Task<IActionResult> RegisterExternalBatch([FromBody] List<GenericModel.RegistrationModel> batch)
        {
            GenericModel.Result result = new GenericModel.Result();
            List<GenericModel.RegisterBatchResult> registerBatchResult = new List<GenericModel.RegisterBatchResult>();
            try
            {
                foreach (var model in batch)
                {
                    CustomerDto customerDto = new CustomerDto()
                    {
                        Username = model.email,
                        Email = model.email,
                        AdminComment = "Generated from External",
                        IsTaxExempt = true,
                        FreeShipping = true,
                        AffiliateId = "",
                        VendorId = "",
                        StoreId = _storeContext.CurrentStore.Id.ToString(),
                        Active = true,
                        Deleted = false,
                        Gender = "",
                        FirstName = model.firstName,
                        LastName = model.lastName,
                        DateOfBirth = DateTime.Now,
                        Company = "-",
                        StreetAddress = "-",
                        StreetAddress2 = "-",
                        ZipPostalCode = "0",
                        City = "-",
                        CountryId = "",
                        StateProvinceId = "",
                        Phone = "-",
                        VatNumber = "",
                        VatNumberStatusId = "",
                        Signature = ""
                    };



                    var registeredRole = await _customerService.GetCustomerRoleBySystemName(SystemCustomerRoleNames.Registered);
                    var newUserRole = await _customerService.GetCustomerRoleBySystemName("New User");

                    var customer = customerDto.ToEntity();
                    customer.CreatedOnUtc = DateTime.UtcNow;
                    customer.LastActivityDateUtc = DateTime.UtcNow;
                    customer.CustomerRoles.Add(registeredRole);
                    customer.CustomerRoles.Add(newUserRole);

                    var country = await _countryService.GetCountryByTwoLetterIsoCode("PH");

                    string saltKey = _encryptionService.CreateSaltKey(5);
                    customer.PasswordFormat = PasswordFormat.Hashed;
                    customer.PasswordSalt = saltKey;
                    customer.Password = _encryptionService.CreatePasswordHash(customer.Password, saltKey, _customerSettings.HashedPasswordFormat);

                    AddCustomerAddressCommand addressCommand = new AddCustomerAddressCommand();
                    addressCommand.Address = new AddressDto();
                    addressCommand.Address.Address1 = "";
                    addressCommand.Address.Address2 = "";
                    addressCommand.Address.VatNumber = "";
                    addressCommand.Address.PhoneNumber = "";
                    addressCommand.Address.City = "";
                    addressCommand.Address.Company = "";
                    addressCommand.Address.CreatedOnUtc = DateTime.Now;
                    addressCommand.Address.Email = model.email;
                    addressCommand.Address.FaxNumber = "";
                    addressCommand.Address.ZipPostalCode = "";
                    addressCommand.Address.FirstName = model.firstName;
                    addressCommand.Address.LastName = model.lastName;
                    addressCommand.Address.City = "";
                    addressCommand.Address.CustomAttributes = "";
                    addressCommand.Address.CountryId = country.Id;
                    addressCommand.Address.StateProvinceId = "";

                    var existingCustomer = await _customerService.GetCustomerByEmail(customer.Email);
                    if (existingCustomer != null)
                    {
                        registerBatchResult.Add(new GenericModel.RegisterBatchResult { email = customer.Email, id = existingCustomer.Id.ToString() });
                        //result = new GenericModel.Result { Message = _localizationService.GetResource("Account.Register.Errors.EmailAlreadyExists"), StatusCode = 500, IsSuccess = false };
                    }
                    else
                    {
                        await _customerService.InsertCustomer(customer);
                        var address = addressCommand.Address.ToEntity();
                        address.CustomerId = customer.Id;
                        await _customerService.InsertAddress(address);
                        registerBatchResult.Add(new GenericModel.RegisterBatchResult { email = customer.Email, id = customer.Id.ToString() });

                        //result = new GenericModel.Result { Message = "User created", StatusCode = 200, IsSuccess = true, ResponseObject = customer.Id.ToString() };
                    }
                }

                result = new GenericModel.Result { Message = "Users created", StatusCode = 200, IsSuccess = true, ResponseObject = Newtonsoft.Json.JsonConvert.SerializeObject(registerBatchResult) };
            }
            catch (Exception ex)
            {
                result = new GenericModel.Result { Message = ex.ToString(), StatusCode = 500, IsSuccess = false };
            }
            return StatusCode(result.StatusCode, result);
        }



        protected async Task<IActionResult> SignInAction(IShoppingCartService shoppingCartService, Customer customer, string returnUrl = null)
        {
            //migrate shopping cart
            await shoppingCartService.MigrateShoppingCart(_workContext.CurrentCustomer, customer, true);

            //sign in new customer
            await _authenticationService.SignIn(customer, true);

            //raise event       
            await _mediator.Publish(new CustomerLoggedinEvent(customer));

            //activity log
            await _customerActivityService.InsertActivity("PublicStore.Login", "", _localizationService.GetResource("ActivityLog.PublicStore.Login"), customer);

            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
                return RedirectToRoute("HomePage");

            return Redirect(returnUrl);
        }

        //available even when a store is closed
        [CheckAccessClosedStore(true)]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> Logout([FromServices] StoreInformationSettings storeInformationSettings)
        {
            if (_workContext.OriginalCustomerIfImpersonated != null)
            {
                //logout impersonated customer
                await _genericAttributeService.SaveAttribute<int?>(_workContext.OriginalCustomerIfImpersonated,
                    SystemCustomerAttributeNames.ImpersonatedCustomerId, null);

                //redirect back to customer details page (admin area)
                return RedirectToAction("Edit", "Customer", new { id = _workContext.CurrentCustomer.Id, area = "Admin" });

            }

            //activity log
            await _customerActivityService.InsertActivity("PublicStore.Logout", "", _localizationService.GetResource("ActivityLog.PublicStore.Logout"));
            //standard logout 
            await _authenticationService.SignOut();

            //EU Cookie
            if (storeInformationSettings.DisplayEuCookieLawWarning)
            {
                //the cookie law message should not pop up immediately after logout.
                //otherwise, the user will have to click it again...
                //and thus next visitor will not click it... so violation for that cookie law..
                //the only good solution in this case is to store a temporary variable
                //indicating that the EU cookie popup window should not be displayed on the next page open (after logout redirection to homepage)
                //but it'll be displayed for further page loads
                TempData["Grand.IgnoreEuCookieLawWarning"] = true;
            }
            return RedirectToRoute("HomePage");
        }

        #endregion

        #region Password recovery

        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual IActionResult PasswordRecovery()
        {
            var model = new PasswordRecoveryModel();
            return View(model);
        }

        [HttpPost, ActionName("PasswordRecovery")]
        [AutoValidateAntiforgeryToken]
        [FormValueRequired("send-email")]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> PasswordRecoverySend(PasswordRecoveryModel model)
        {
            if (ModelState.IsValid)
            {
                Random generator = new Random();
                string newPassword = generator.Next(0, 1000000).ToString("D6");

                var customer = await _customerService.GetCustomerByEmail(model.Email);
                if (customer != null && customer.Active && !customer.Deleted)
                {
                    customer.TempPassword = newPassword;
                    await _mediator.Send(new PasswordRecoverySendCommand() { Customer = customer, Language = _workContext.WorkingLanguage, Model = model });
                    model.Result = _localizationService.GetResource("Account.PasswordRecovery.EmailHasBeenSent");

                    var response = await _customerRegistrationService.ChangePassword(new ChangePasswordRequest(model.Email,
                    false, _customerSettings.DefaultPasswordFormat, newPassword));
                }
                else
                {
                    model.Result = _localizationService.GetResource("Account.PasswordRecovery.EmailNotFound");
                }

                return View(model);
            }

            //If we got this far, something failed, redisplay form
            return View(model);
        }

        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> PasswordRecoveryConfirm(string token, string email)
        {
            var customer = await _customerService.GetCustomerByEmail(email);
            if (customer == null)
                return RedirectToRoute("HomePage");

            var model = await _mediator.Send(new GetPasswordRecoveryConfirm() { Customer = customer, Token = token });

            return View(model);
        }

        [HttpPost, ActionName("PasswordRecoveryConfirm")]
        [AutoValidateAntiforgeryToken]
        [FormValueRequired("set-password")]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> PasswordRecoveryConfirmPOST(string token, string email, PasswordRecoveryConfirmModel model)
        {
            var customer = await _customerService.GetCustomerByEmail(email);
            if (customer == null)
                return RedirectToRoute("HomePage");

            //validate token
            if (!customer.IsPasswordRecoveryTokenValid(token))
            {
                model.DisablePasswordChanging = true;
                model.Result = _localizationService.GetResource("Account.PasswordRecovery.WrongToken");
            }

            //validate token expiration date
            if (customer.IsPasswordRecoveryLinkExpired(_customerSettings))
            {
                model.DisablePasswordChanging = true;
                model.Result = _localizationService.GetResource("Account.PasswordRecovery.LinkExpired");
                return View(model);
            }

            if (ModelState.IsValid)
            {
                var response = await _customerRegistrationService.ChangePassword(new ChangePasswordRequest(email,
                    false, _customerSettings.DefaultPasswordFormat, model.NewPassword));
                if (response.Success)
                {
                    await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.PasswordRecoveryToken, "");

                    model.DisablePasswordChanging = true;
                    model.Result = _localizationService.GetResource("Account.PasswordRecovery.PasswordHasBeenChanged");
                }
                else
                {
                    model.Result = response.Errors.FirstOrDefault();
                }

                return View(model);
            }

            //If we got this far, something failed, redisplay form
            return View(model);
        }

        #endregion

        #region Register

        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> Register()
        {
            //check whether registration is allowed
            if (_customerSettings.UserRegistrationType == UserRegistrationType.Disabled)
                return RedirectToRoute("RegisterResult", new { resultId = (int)UserRegistrationType.Disabled });

            var model = await _mediator.Send(new GetRegister() {
                Customer = _workContext.CurrentCustomer,
                ExcludeProperties = false,
                Language = _workContext.WorkingLanguage,
                Store = _storeContext.CurrentStore
            });

            return View(model);
        }

        [HttpPost]
        [ValidateCaptcha]
        [ValidateHoneypot]
        [AutoValidateAntiforgeryToken]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> Register(RegisterModel model, string returnUrl, bool captchaValid, IFormCollection form,
           [FromServices] IWebHelper webHelper, [FromServices] ICustomerAttributeParser customerAttributeParser)
        {
            //check whether registration is allowed
            if (_customerSettings.UserRegistrationType == UserRegistrationType.Disabled)
                return RedirectToRoute("RegisterResult", new { resultId = (int)UserRegistrationType.Disabled });

            if (_workContext.CurrentCustomer.IsRegistered())
            {
                //Already registered customer. 
                await _authenticationService.SignOut();

                //Save a new record
                _workContext.CurrentCustomer = await _customerService.InsertGuestCustomer(_storeContext.CurrentStore);
            }
            //custom customer attributes
            var customerAttributesXml = await _mediator.Send(new GetParseCustomAttributes() { Form = form });
            var customerAttributeWarnings = await customerAttributeParser.GetAttributeWarnings(customerAttributesXml);
            foreach (var error in customerAttributeWarnings)
            {
                ModelState.AddModelError("", error);
            }

            //validate CAPTCHA
            if (_captchaSettings.Enabled && _captchaSettings.ShowOnRegistrationPage && !captchaValid)
            {
                ModelState.AddModelError("", _captchaSettings.GetWrongCaptchaMessage(_localizationService));
            }

            if (ModelState.IsValid && ModelState.ErrorCount == 0)
            {
                if (_customerSettings.UsernamesEnabled && model.Username != null)
                {
                    model.Username = model.Username.Trim();
                }

                bool isApproved = _customerSettings.UserRegistrationType == UserRegistrationType.Standard;
                var registrationRequest = new CustomerRegistrationRequest(_workContext.CurrentCustomer, model.Email,
                    _customerSettings.UsernamesEnabled ? model.Username : model.Email, model.Password,
                    _customerSettings.DefaultPasswordFormat, _storeContext.CurrentStore.Id, isApproved);
                var registrationResult = await _customerRegistrationService.RegisterCustomer(registrationRequest);
                if (registrationResult.Success)
                {
                    await _mediator.Send(new CustomerRegisteredCommand() {
                        Customer = _workContext.CurrentCustomer,
                        CustomerAttributesXml = customerAttributesXml,
                        Form = form,
                        Model = model,
                        Store = _storeContext.CurrentStore
                    });

                    //login customer now
                    if (isApproved)
                        await _authenticationService.SignIn(_workContext.CurrentCustomer, true);

                    //raise event       
                    await _mediator.Publish(new CustomerRegisteredEvent(_workContext.CurrentCustomer));

                    _workContext.CurrentCustomer.TempPassword = model.Password;
                    switch (_customerSettings.UserRegistrationType)
                    {
                        case UserRegistrationType.EmailValidation:
                            {
                                //email validation message
                                await _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, SystemCustomerAttributeNames.AccountActivationToken, Guid.NewGuid().ToString());
                                await _workflowMessageService.SendCustomerEmailValidationMessage(_workContext.CurrentCustomer, _workContext.WorkingLanguage.Id);

                                //result
                                return RedirectToRoute("RegisterResult", new { resultId = (int)UserRegistrationType.EmailValidation });
                            }
                        case UserRegistrationType.AdminApproval:
                            {
                                return RedirectToRoute("RegisterResult", new { resultId = (int)UserRegistrationType.AdminApproval });
                            }
                        case UserRegistrationType.Standard:
                            {
                                //send customer welcome message
                                await _workflowMessageService.SendCustomerWelcomeMessage(_workContext.CurrentCustomer, _workContext.WorkingLanguage.Id);

                                var redirectUrl = Url.RouteUrl("RegisterResult", new { resultId = (int)UserRegistrationType.Standard }, HttpContext.Request.Scheme);
                                if (!String.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                                {
                                    redirectUrl = webHelper.ModifyQueryString(redirectUrl, "returnurl", returnUrl);
                                }
                                return Redirect(redirectUrl);
                            }
                        default:
                            {
                                return RedirectToRoute("HomePage");
                            }
                    }
                }

                //errors
                foreach (var error in registrationResult.Errors)
                    ModelState.AddModelError("", error);
            }

            //If we got this far, something failed, redisplay form
            model = await _mediator.Send(new GetRegister() {
                Customer = _workContext.CurrentCustomer,
                ExcludeProperties = true,
                Language = _workContext.WorkingLanguage,
                Store = _storeContext.CurrentStore,
                Model = model,
                OverrideCustomCustomerAttributesXml = customerAttributesXml
            });

            return View(model);
        }
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual IActionResult RegisterResult(int resultId)
        {
            var resultText = "";
            switch ((UserRegistrationType)resultId)
            {
                case UserRegistrationType.Disabled:
                    resultText = _localizationService.GetResource("Account.Register.Result.Disabled");
                    break;
                case UserRegistrationType.Standard:
                    resultText = _localizationService.GetResource("Account.Register.Result.Standard");
                    break;
                case UserRegistrationType.AdminApproval:
                    resultText = _localizationService.GetResource("Account.Register.Result.AdminApproval");
                    break;
                case UserRegistrationType.EmailValidation:
                    resultText = _localizationService.GetResource("Account.Register.Result.EmailValidation");
                    break;
                default:
                    break;
            }
            var model = new RegisterResultModel {
                Result = resultText
            };
            return View(model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> CheckUsernameAvailability(string username)
        {
            var usernameAvailable = false;
            var statusText = _localizationService.GetResource("Account.CheckUsernameAvailability.NotAvailable");

            if (_customerSettings.UsernamesEnabled && !String.IsNullOrWhiteSpace(username))
            {
                if (_workContext.CurrentCustomer != null &&
                    _workContext.CurrentCustomer.Username != null &&
                    _workContext.CurrentCustomer.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                {
                    statusText = _localizationService.GetResource("Account.CheckUsernameAvailability.CurrentUsername");
                }
                else
                {
                    var customer = await _customerService.GetCustomerByUsername(username);
                    if (customer == null)
                    {
                        statusText = _localizationService.GetResource("Account.CheckUsernameAvailability.Available");
                        usernameAvailable = true;
                    }
                }
            }

            return Json(new { Available = usernameAvailable, Text = statusText });
        }

        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> AccountActivation(string token, string email)
        {
            var customer = await _customerService.GetCustomerByEmail(email);
            if (customer == null)
                return RedirectToRoute("HomePage");

            var cToken = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.AccountActivationToken);
            if (String.IsNullOrEmpty(cToken))
                return RedirectToRoute("HomePage");

            if (!cToken.Equals(token, StringComparison.OrdinalIgnoreCase))
                return RedirectToRoute("HomePage");

            //activate user account
            customer.Active = true;
            customer.StoreId = _storeContext.CurrentStore.Id;
            await _customerService.UpdateActive(customer);
            await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.AccountActivationToken, "");

            //send welcome message
            await _workflowMessageService.SendCustomerWelcomeMessage(customer, _workContext.WorkingLanguage.Id);

            var model = new AccountActivationModel();
            model.Result = _localizationService.GetResource("Account.AccountActivation.Activated");
            return View(model);
        }

        #endregion

        #region My account / Info

        public virtual async Task<IActionResult> Info()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var model = await _mediator.Send(new GetInfo() {
                Customer = _workContext.CurrentCustomer,
                ExcludeProperties = false,
                Language = _workContext.WorkingLanguage,
                Store = _storeContext.CurrentStore,
            });
            return View(model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public virtual async Task<IActionResult> Info(CustomerInfoModel model, IFormCollection form,
            [FromServices] ICustomerAttributeParser customerAttributeParser)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            //custom customer attributes
            var customerAttributesXml = await _mediator.Send(new GetParseCustomAttributes() { Form = form });
            var customerAttributeWarnings = await customerAttributeParser.GetAttributeWarnings(customerAttributesXml);

            foreach (var error in customerAttributeWarnings)
            {
                ModelState.AddModelError("", error);
            }

            try
            {
                if (ModelState.IsValid && ModelState.ErrorCount == 0)
                {
                    await _mediator.Send(new UpdateCustomerInfoCommand() {
                        Customer = _workContext.CurrentCustomer,
                        CustomerAttributesXml = customerAttributesXml,
                        Form = form,
                        Model = model,
                        OriginalCustomerIfImpersonated = _workContext.OriginalCustomerIfImpersonated,
                        Store = _storeContext.CurrentStore
                    });
                    return RedirectToRoute("CustomerInfo");
                }
            }
            catch (Exception exc)
            {
                ModelState.AddModelError("", exc.Message);
            }

            //If we got this far, something failed, redisplay form
            model = await _mediator.Send(new GetInfo() {
                Model = model,
                Customer = _workContext.CurrentCustomer,
                ExcludeProperties = true,
                Language = _workContext.WorkingLanguage,
                Store = _storeContext.CurrentStore,
                OverrideCustomCustomerAttributesXml = customerAttributesXml
            });

            return View(model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public virtual async Task<IActionResult> RemoveExternalAssociation(string id, [FromServices] IExternalAuthenticationService openAuthenticationService)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            //ensure it's our record
            var ear = (await openAuthenticationService.GetExternalIdentifiersFor(_workContext.CurrentCustomer))
                .FirstOrDefault(x => x.Id == id);

            if (ear == null)
            {
                return Json(new
                {
                    redirect = Url.Action("Info"),
                });
            }
            await openAuthenticationService.DeleteExternalAuthenticationRecord(ear);

            return Json(new
            {
                redirect = Url.Action("Info"),
            });
        }


        public virtual async Task<IActionResult> Export([FromServices] IExportManager exportManager)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (!_customerSettings.AllowUsersToExportData)
                return Challenge();

            var customer = _workContext.CurrentCustomer;
            byte[] bytes = await exportManager.ExportCustomerToXlsx(customer, _storeContext.CurrentStore.Id);
            return File(bytes, "text/xls", "PersonalInfo.xlsx");

        }
        #endregion

        #region My account / Addresses

        public virtual async Task<IActionResult> Addresses()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var model = await _mediator.Send(new GetAddressList() { 
                Customer = _workContext.CurrentCustomer, 
                Language = _workContext.WorkingLanguage,
                Store = _storeContext.CurrentStore
            });
            return View(model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public virtual async Task<IActionResult> AddressDelete(string addressId)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var customer = _workContext.CurrentCustomer;

            //find address (ensure that it belongs to the current customer)
            var address = customer.Addresses.FirstOrDefault(a => a.Id == addressId);
            if (address != null)
            {
                customer.RemoveAddress(address);
                address.CustomerId = customer.Id;
                await _customerService.DeleteAddress(address);
            }

            return Json(new
            {
                redirect = Url.RouteUrl("CustomerAddresses"),
            });

        }

        public virtual async Task<IActionResult> AddressAdd()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var countries = await _countryService.GetAllCountries(_workContext.WorkingLanguage.Id);
            var model = new CustomerAddressEditModel {
                Address = await _mediator.Send(new GetAddressModel() {
                    Language = _workContext.WorkingLanguage,
                    Store = _storeContext.CurrentStore,
                    Model = null,
                    Address = null,
                    ExcludeProperties = false,
                    LoadCountries = () => countries
                })
            };

            return View(model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public virtual async Task<IActionResult> AddressAdd(CustomerAddressEditModel model, IFormCollection form,
            [FromServices] IAddressAttributeParser addressAttributeParser)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var customer = _workContext.CurrentCustomer;

            //custom address attributes
            var customAttributes = await _mediator.Send(new GetParseCustomAddressAttributes() { Form = form });
            var customAttributeWarnings = await addressAttributeParser.GetAttributeWarnings(customAttributes);
            foreach (var error in customAttributeWarnings)
            {
                ModelState.AddModelError("", error);
            }

            if (ModelState.IsValid && ModelState.ErrorCount == 0)
            {
                var address = model.Address.ToEntity();
                address.CustomAttributes = customAttributes;
                address.CreatedOnUtc = DateTime.UtcNow;
                customer.Addresses.Add(address);
                address.CustomerId = customer.Id;

                await _customerService.InsertAddress(address);

                return RedirectToRoute("CustomerAddresses");
            }
            var countries = await _countryService.GetAllCountries(_workContext.WorkingLanguage.Id);
            //If we got this far, something failed, redisplay form
            model.Address = await _mediator.Send(new GetAddressModel() {
                Language = _workContext.WorkingLanguage,
                Store = _storeContext.CurrentStore,
                Model = model.Address,
                Address = null,
                ExcludeProperties = true,
                LoadCountries = () => countries
            });

            return View(model);
        }

        public virtual async Task<IActionResult> AddressEdit(string addressId)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var customer = _workContext.CurrentCustomer;
            //find address (ensure that it belongs to the current customer)
            var address = customer.Addresses.FirstOrDefault(a => a.Id == addressId);
            if (address == null)
                //address is not found
                return RedirectToRoute("CustomerAddresses");

            var countries = await _countryService.GetAllCountries(_workContext.WorkingLanguage.Id);
            var model = new CustomerAddressEditModel();
            model.Address = await _mediator.Send(new GetAddressModel() {
                Language = _workContext.WorkingLanguage,
                Store = _storeContext.CurrentStore,
                Model = model.Address,
                Address = address,
                ExcludeProperties = false,
                LoadCountries = () => countries
            });

            return View(model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public virtual async Task<IActionResult> AddressEdit(CustomerAddressEditModel model, string addressId, IFormCollection form, 
            [FromServices] IAddressAttributeParser addressAttributeParser)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var customer = _workContext.CurrentCustomer;
            //find address (ensure that it belongs to the current customer)
            var address = customer.Addresses.FirstOrDefault(a => a.Id == addressId);
            if (address == null)
                //address is not found
                return RedirectToRoute("CustomerAddresses");

            //custom address attributes
            var customAttributes = await _mediator.Send(new GetParseCustomAddressAttributes() { Form = form });
            var customAttributeWarnings = await addressAttributeParser.GetAttributeWarnings(customAttributes);
            foreach (var error in customAttributeWarnings)
            {
                ModelState.AddModelError("", error);
            }

            if (ModelState.IsValid && ModelState.ErrorCount == 0)
            {
                address = model.Address.ToEntity(address);
                address.CustomAttributes = customAttributes;
                address.CustomerId = customer.Id;
                await _customerService.UpdateAddress(address);

                if (customer.BillingAddress?.Id == address.Id)
                    await _customerService.UpdateBillingAddress(address);
                if (customer.ShippingAddress?.Id == address.Id)
                    await _customerService.UpdateShippingAddress(address);

                return RedirectToRoute("CustomerAddresses");
            }
            var countries = await _countryService.GetAllCountries(_workContext.WorkingLanguage.Id);
            //If we got this far, something failed, redisplay form
            model.Address = await _mediator.Send(new GetAddressModel() {
                Language = _workContext.WorkingLanguage,
                Store = _storeContext.CurrentStore,
                Model = model.Address,
                Address = address,
                ExcludeProperties = true,
                LoadCountries = () => countries
            });

            return View(model);
        }

        #endregion

        #region My account / Downloadable products

        public virtual async Task<IActionResult> DownloadableProducts()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (_customerSettings.HideDownloadableProductsTab)
                return RedirectToRoute("CustomerInfo");

            var model = await _mediator.Send(new GetDownloadableProducts() { Customer = _workContext.CurrentCustomer, Language = _workContext.WorkingLanguage });
            return View(model);
        }

        public virtual async Task<IActionResult> UserAgreement(Guid orderItemId)
        {
            var model = await _mediator.Send(new GetUserAgreement() { OrderItemId = orderItemId }); ;
            if (model == null)
                return RedirectToRoute("HomePage");

            return View(model);
        }

        #endregion

        #region My account / Change password

        public virtual IActionResult ChangePassword([FromServices] CustomerSettings customerSettings)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var model = new ChangePasswordModel();

            //display the cause of the change password 
            if (_workContext.CurrentCustomer.PasswordIsExpired(customerSettings))
                ModelState.AddModelError(string.Empty, _localizationService.GetResource("Account.ChangePassword.PasswordIsExpired"));

            return View(model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public virtual async Task<IActionResult> ChangePassword(ChangePasswordModel model)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var customer = _workContext.CurrentCustomer;

            if (ModelState.IsValid)
            {
                var changePasswordRequest = new ChangePasswordRequest(customer.Email,
                    true, _customerSettings.DefaultPasswordFormat, model.NewPassword, model.OldPassword);
                var changePasswordResult = await _customerRegistrationService.ChangePassword(changePasswordRequest);
                if (changePasswordResult.Success)
                {
                    model.Result = _localizationService.GetResource("Account.ChangePassword.Success");
                    return View(model);
                }

                //errors
                foreach (var error in changePasswordResult.Errors)
                    ModelState.AddModelError("", error);
            }

            //If we got this far, something failed, redisplay form
            return View(model);
        }

        #endregion

        #region My account / Delete account

        public virtual IActionResult DeleteAccount()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (!_customerSettings.AllowUsersToDeleteAccount)
                return RedirectToRoute("CustomerInfo");

            var model = new DeleteAccountModel();

            return View(model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public virtual async Task<IActionResult> DeleteAccount(DeleteAccountModel model)
        {
            var customer = _workContext.CurrentCustomer;
            if (!customer.IsRegistered())
                return Challenge();

            if (!_customerSettings.AllowUsersToDeleteAccount)
                return RedirectToRoute("CustomerInfo");

            if (ModelState.IsValid)
            {
                var loginResult = await _customerRegistrationService.ValidateCustomer(_customerSettings.UsernamesEnabled ? customer.Username : customer.Email, model.Password);

                switch (loginResult)
                {
                    case CustomerLoginResults.Successful:
                        {
                            //activity log
                            await _customerActivityService.InsertActivity("PublicStore.DeleteAccount", "", _localizationService.GetResource("ActivityLog.DeleteAccount"));

                            //delete account 
                            await _mediator.Send(new DeleteAccountCommand() { Customer = customer, Store = _storeContext.CurrentStore });

                            //standard logout 
                            await _authenticationService.SignOut();

                            return RedirectToRoute("HomePage");
                        }
                    case CustomerLoginResults.CustomerNotExist:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.CustomerNotExist"));
                        break;
                    case CustomerLoginResults.Deleted:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.Deleted"));
                        break;
                    case CustomerLoginResults.NotActive:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.NotActive"));
                        break;
                    case CustomerLoginResults.NotRegistered:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.NotRegistered"));
                        break;
                    case CustomerLoginResults.LockedOut:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials.LockedOut"));
                        break;
                    case CustomerLoginResults.WrongPassword:
                    default:
                        ModelState.AddModelError("", _localizationService.GetResource("Account.Login.WrongCredentials"));
                        break;
                }
            }

            //If we got this far, something failed, redisplay form
            return View(model);
        }

        #endregion

        #region My account / Avatar

        public virtual async Task<IActionResult> Avatar()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (!_customerSettings.AllowCustomersToUploadAvatars)
                return RedirectToRoute("CustomerInfo");

            var model = await _mediator.Send(new GetAvatar() { Customer = _workContext.CurrentCustomer });

            return View(model);
        }

        [HttpPost, ActionName("Avatar")]
        [AutoValidateAntiforgeryToken]
        [FormValueRequired("upload-avatar")]
        public virtual async Task<IActionResult> UploadAvatar(CustomerAvatarModel model, IFormFile uploadedFile)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (!_customerSettings.AllowCustomersToUploadAvatars)
                return RedirectToRoute("CustomerInfo");

            if (ModelState.IsValid)
            {
                try
                {
                    await _mediator.Send(new UploadAvatarCommand() { Customer = _workContext.CurrentCustomer, UploadedFile = uploadedFile });

                    model = await _mediator.Send(new GetAvatar() { Customer = _workContext.CurrentCustomer });
                    return View(model);
                }
                catch (Exception exc)
                {
                    ModelState.AddModelError("", exc.Message);
                }
            }

            //If we got this far, something failed, redisplay form
            model = await _mediator.Send(new GetAvatar() { Customer = _workContext.CurrentCustomer });

            return View(model);
        }

        [HttpPost, ActionName("Avatar")]
        [AutoValidateAntiforgeryToken]
        [FormValueRequired("remove-avatar")]
        public virtual async Task<IActionResult> RemoveAvatar(CustomerAvatarModel model)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (!_customerSettings.AllowCustomersToUploadAvatars)
                return RedirectToRoute("CustomerInfo");

            await _mediator.Send(new UploadAvatarCommand() { Customer = _workContext.CurrentCustomer, Remove = true });

            return RedirectToRoute("CustomerAvatar");
        }

        #endregion

        #region My account / Auctions

        public virtual async Task<IActionResult> Auctions()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (_customerSettings.HideAuctionsTab)
                return RedirectToRoute("CustomerInfo");

            var model = await _mediator.Send(new GetAuctions() { Customer = _workContext.CurrentCustomer, Language = _workContext.WorkingLanguage });

            return View(model);
        }

        #endregion

        #region My account / Notes

        public virtual async Task<IActionResult> Notes()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (_customerSettings.HideNotesTab)
                return RedirectToRoute("CustomerInfo");

            var model = await _mediator.Send(new GetNotes() { Customer = _workContext.CurrentCustomer });

            return View(model);
        }

        #endregion

        #region My account / Documents

        public virtual async Task<IActionResult> Documents()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (_customerSettings.HideDocumentsTab)
                return RedirectToRoute("CustomerInfo");

            var model = await _mediator.Send(new GetDocuments() { Customer = _workContext.CurrentCustomer, Language = _workContext.WorkingLanguage });

            return View(model);
        }

        #endregion

        #region My account / Reviews

        public virtual async Task<IActionResult> Reviews()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (_customerSettings.HideReviewsTab)
                return RedirectToRoute("CustomerInfo");

            var model = await _mediator.Send(new GetReviews() { Customer = _workContext.CurrentCustomer, Language = _workContext.WorkingLanguage });

            return View(model);
        }

        #endregion

        #region My account / TwoFactorAuth

        public async Task<IActionResult> EnableTwoFactorAuthenticator()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (!_customerSettings.TwoFactorAuthenticationEnabled)
                return RedirectToRoute("CustomerInfo");

            if (_workContext.CurrentCustomer.GetAttributeFromEntity<bool>(SystemCustomerAttributeNames.TwoFactorEnabled))
                return RedirectToRoute("CustomerInfo");

            var model = await _mediator.Send(new GetTwoFactorAuthentication());
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EnableTwoFactorAuthenticator(CustomerInfoModel.TwoFactorAuthenticationModel model,
            [FromServices] ITwoFactorAuthenticationService twoFactorAuthenticationService)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (!_customerSettings.TwoFactorAuthenticationEnabled)
                return RedirectToRoute("CustomerInfo");

            if (_workContext.CurrentCustomer.GetAttributeFromEntity<bool>(SystemCustomerAttributeNames.TwoFactorEnabled))
                return RedirectToRoute("CustomerInfo");

            if (string.IsNullOrEmpty(model.Code))
            {
                ModelState.AddModelError("", _localizationService.GetResource("Account.TwoFactorAuth.SecurityCodeIsRequired"));
            }
            else
            {
                if (twoFactorAuthenticationService.AuthenticateTwoFactor(model.SecretKey, model.Code))
                {
                    await _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, SystemCustomerAttributeNames.TwoFactorEnabled, true);
                    await _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, SystemCustomerAttributeNames.TwoFactorSecretKey, model.SecretKey);

                    SuccessNotification(_localizationService.GetResource("Account.TwoFactorAuth.Enabled"));

                    return RedirectToRoute("CustomerInfo");
                }
                ModelState.AddModelError("", _localizationService.GetResource("Account.TwoFactorAuth.WrongSecurityCode"));
            }

            return View(model);
        }


        public async Task<IActionResult> DisableTwoFactorAuthenticator()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (!_customerSettings.TwoFactorAuthenticationEnabled)
                return RedirectToRoute("CustomerInfo");

            var customer = _workContext.CurrentCustomer;
            if (customer != null)
            {
                await _genericAttributeService.SaveAttribute<bool>(customer, SystemCustomerAttributeNames.TwoFactorEnabled, false);
                await _genericAttributeService.SaveAttribute<string>(customer, SystemCustomerAttributeNames.TwoFactorSecretKey, null);

                SuccessNotification(_localizationService.GetResource("Account.TwoFactorAuth.Disabled"));

                return RedirectToRoute("CustomerInfo");
            }
            return View();
        }


        #endregion

        #region My account / Courses

        public virtual async Task<IActionResult> Courses()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            if (_customerSettings.HideCoursesTab)
                return RedirectToRoute("CustomerInfo");

            var model = await _mediator.Send(new GetCourses() { Customer = _workContext.CurrentCustomer, Store = _storeContext.CurrentStore });

            return View(model);
        }

        #endregion


    }
}
