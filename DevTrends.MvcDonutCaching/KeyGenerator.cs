using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace DevTrends.MvcDonutCaching
{
    public class KeyGenerator : IKeyGenerator
    {
        private readonly IKeyBuilder _keyBuilder;
        private ILog log = LogManager.GetLogger(typeof(KeyGenerator));

        public KeyGenerator(IKeyBuilder keyBuilder)
        {
            if (keyBuilder == null)
            {
                throw new ArgumentNullException("keyBuilder");
            }

            _keyBuilder = keyBuilder;
        }

        public string GenerateKey(ControllerContext context, CacheSettings cacheSettings)
        {
            var debugTraceBuilder = new StringBuilder();

            try
            {

                debugTraceBuilder.AppendLine("Retrieving Route Data Values");

                var actionName = context.RouteData.Values["action"].ToString();
                debugTraceBuilder.AppendLine(string.Format("Retrieved Action Name: {0}", actionName));

                var controllerName = context.RouteData.Values["controller"].ToString();
                debugTraceBuilder.AppendLine(string.Format("Retrieved Controller Name: {0}", controllerName));

                string areaName = null;

                if (context.RouteData.DataTokens.ContainsKey("area"))
                {
                    areaName = context.RouteData.DataTokens["area"].ToString();
                    debugTraceBuilder.AppendLine(string.Format("Retrieved Area Name: {0}", areaName));
                }

                debugTraceBuilder.AppendLine("Retrieving Route Data Values Completed");

                debugTraceBuilder.AppendLine("Filtering Route Data");

                // remove controller, action and DictionaryValueProvider which is added by the framework for child actions
                var filteredRouteData = context.RouteData.Values.Where(
                    x => x.Key.ToLowerInvariant() != "controller" &&
                         x.Key.ToLowerInvariant() != "action" &&
                         x.Key.ToLowerInvariant() != "area" &&
                         !(x.Value is DictionaryValueProvider<object>)
                ).ToList();

                debugTraceBuilder.AppendLine("Filtering Route Data Completed");

                if (!string.IsNullOrWhiteSpace(areaName))
                {
                    debugTraceBuilder.AppendLine("Re-Adding Area to Filtered Route Data");
                    filteredRouteData.Add(new KeyValuePair<string, object>("area", areaName));
                    debugTraceBuilder.AppendLine("Re-Adding Area to Filtered Route Data Completed");
                }

                debugTraceBuilder.AppendLine("Creating RouteValueDictionary from Filtered Route Data");
                var routeValues = new RouteValueDictionary(filteredRouteData.ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Value));
                debugTraceBuilder.AppendLine("Creating RouteValueDictionary from Filtered Route Data Completed");

                if (!context.IsChildAction)
                {
                    debugTraceBuilder.AppendLine("Processing As Non-Child Action");
                    // note that route values take priority over form values and form values take priority over query string values

                    if ((cacheSettings.Options & OutputCacheOptions.IgnoreFormData) != OutputCacheOptions.IgnoreFormData)
                    {
                        debugTraceBuilder.AppendLine("Processing FormData");

                        foreach (var formKey in context.HttpContext.Request.Form.AllKeys)
                        {
                            debugTraceBuilder.AppendLine(string.Format("Processing FormData With Key: '{0}'", formKey));

                            if (routeValues.ContainsKey(formKey.ToLowerInvariant()))
                            {
                                debugTraceBuilder.AppendLine(string.Format("RouteValues already contains data with Key: '{0}', so skipping FormData value", formKey));
                                continue;
                            }

                            debugTraceBuilder.AppendLine(string.Format("Adding FormData With Key: '{0}' to RouteValues", formKey));

                            var item = context.HttpContext.Request.Form[formKey];
                            routeValues.Add(
                                formKey.ToLowerInvariant(),
                                item != null
                                    ? item.ToLowerInvariant()
                                    : string.Empty
                            );

                            debugTraceBuilder.AppendLine(string.Format("Adding FormData With Key: '{0}' to RouteValues Completed", formKey));
                            debugTraceBuilder.AppendLine(string.Format("Processing FormData With Key: '{0}' Completed", formKey));
                        }

                        debugTraceBuilder.AppendLine("Processing FormData Completed");
                    }

                    if ((cacheSettings.Options & OutputCacheOptions.IgnoreQueryString) != OutputCacheOptions.IgnoreQueryString)
                    {
                        debugTraceBuilder.AppendLine("Processing QueryString");

                        foreach (var queryStringKey in context.HttpContext.Request.QueryString.AllKeys)
                        {
                            debugTraceBuilder.AppendLine(string.Format("Processing QueryString With Key: '{0}'", queryStringKey));

                            // queryStringKey is null if url has qs name without value. e.g. test.com?q
                            if (queryStringKey == null || routeValues.ContainsKey(queryStringKey.ToLowerInvariant()))
                            {
                                debugTraceBuilder.AppendLine(string.Format("RouteValues already contains data with Key: '{0}', so skipping QueryString value", queryStringKey));
                                continue;
                            }

                            debugTraceBuilder.AppendLine(string.Format("Adding QueryString with Key: '{0}' to RouteValues", queryStringKey));

                            var item = context.HttpContext.Request.QueryString[queryStringKey];
                            routeValues.Add(
                                queryStringKey.ToLowerInvariant(),
                                item != null
                                    ? item.ToLowerInvariant()
                                    : string.Empty
                            );

                            debugTraceBuilder.AppendLine(string.Format("Adding QueryString With Key: '{0}' to RouteValues Completed", queryStringKey));
                            debugTraceBuilder.AppendLine(string.Format("Processing QueryString With Key: '{0}' Completed", queryStringKey));
                        }

                        debugTraceBuilder.AppendLine("Processing QueryString Completed");
                    }

                    debugTraceBuilder.AppendLine("Processing As Non-Child Action Completed");
                }

                if (!string.IsNullOrEmpty(cacheSettings.VaryByParam))
                {
                    debugTraceBuilder.AppendLine("Processing VaryByParam");

                    if (cacheSettings.VaryByParam.ToLowerInvariant() == "none")
                    {
                        debugTraceBuilder.AppendLine("Clearing RouteValues since VaryByParam set to 'none'");
                        routeValues.Clear();
                        debugTraceBuilder.AppendLine("Clearing RouteValues Completed");
                    }
                    else if (cacheSettings.VaryByParam != "*")
                    {
                        debugTraceBuilder.AppendLine("Extracting VaryByParam Parameters");
                        var parameters = cacheSettings.VaryByParam.ToLowerInvariant().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        debugTraceBuilder.AppendLine(string.Format("Extracting VaryByParam Parameters Completed. Parameters: {0}", parameters));

                        debugTraceBuilder.AppendLine("Setting RouteValues To Only Extracted Parameters");
                        routeValues = new RouteValueDictionary(routeValues.Where(x => parameters.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value));
                        debugTraceBuilder.AppendLine("Setting RouteValues To Only Extracted Parameters Completed");
                    }

                    debugTraceBuilder.AppendLine("Processing VaryByParam Completed");
                }

                if (!string.IsNullOrEmpty(cacheSettings.VaryByCustom))
                {
                    debugTraceBuilder.AppendLine("Processing VaryByCustom");

                    routeValues.Add(
                        cacheSettings.VaryByCustom.ToLowerInvariant(),
                        context.HttpContext.ApplicationInstance.GetVaryByCustomString(HttpContext.Current, cacheSettings.VaryByCustom)
                    );

                    debugTraceBuilder.AppendLine("Processing VaryByCustom Completed");
                }

                debugTraceBuilder.AppendLine(string.Format("Building Key with Controller: {0}, Action: {1}, RouteValues: {2}", controllerName, actionName, routeValues));
                var key = _keyBuilder.BuildKey(controllerName, actionName, routeValues);
                debugTraceBuilder.AppendLine("Building Key Completed");

                return key;
            }
            catch(Exception ex)
            {
                log.Error(debugTraceBuilder, ex);
                throw;
            }
        }
    }
}
