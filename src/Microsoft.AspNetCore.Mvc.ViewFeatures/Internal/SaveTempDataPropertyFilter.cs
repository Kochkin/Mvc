// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Mvc.ViewFeatures.Internal
{
    public class SaveTempDataPropertyFilter : ISaveTempDataCallback, IActionFilter
    {
        private const string Prefix = "TempDataProperty-";
        private readonly ITempDataDictionaryFactory _factory;

        public SaveTempDataPropertyFilter(ITempDataDictionaryFactory factory)
        {
            _factory = factory;
        }

        // Cannot be public as <c>PropertyHelper</c> is an internal shared source type
        internal IList<PropertyHelper> PropertyHelpers { get; set; }

        public object Subject { get; set; }

        public IDictionary<PropertyInfo, object> OriginalValues { get; set; }

        /// <summary>
        /// Puts the modified values of <see cref="Subject"/> into <paramref name="tempData"/>.
        /// </summary>
        /// <param name="tempData">The <see cref="ITempDataDictionary"/> to be updated.</param>
        public void OnTempDataSaving(ITempDataDictionary tempData)
        {
            if (Subject != null && OriginalValues != null)
            {
                foreach (var kvp in OriginalValues)
                {
                    var property = kvp.Key;
                    var originalValue = kvp.Value;

                    var newValue = property.GetValue(Subject);
                    if (newValue != null && !newValue.Equals(originalValue))
                    {
                        tempData[Prefix + property.Name] = newValue;
                    }
                }
            }
        }

        /// <summary>
        /// Applies values from TempData from <paramref name="httpContext"/> to the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The properties of the subject are set based on TempData from
        /// <paramref name="httpContext"/>. May be a <see cref="Controller"/>.</param>
        /// <param name="httpContext">The <see cref="HttpContext"/> used to find TempData.</param>
        public void ApplyTempDataChanges(object subject, HttpContext httpContext)
        {
            var tempData = _factory.GetTempData(httpContext);

            SetPropertyVaules(tempData, subject);
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (PropertyHelpers == null)
            {
                throw new ArgumentNullException(nameof(PropertyHelpers));
            }

            Subject = context.Controller;
            var tempData = _factory.GetTempData(context.HttpContext);

            OriginalValues = new Dictionary<PropertyInfo, object>();

            SetPropertyVaules(tempData, Subject);
        }

        private void SetPropertyVaules(ITempDataDictionary tempData, object subject)
        {
            if (PropertyHelpers == null)
            {
                return;
            }

            if (OriginalValues == null)
            {
                OriginalValues = new Dictionary<PropertyInfo, object>();
            }

            for (var i = 0; i < PropertyHelpers.Count; i++)
            {
                var property = PropertyHelpers[i].Property;
                var value = tempData[Prefix + property.Name];

                OriginalValues[property] = value;

                var propertyTypeInfo = property.PropertyType.GetTypeInfo();

                var isReferenceTypeOrNullable = !propertyTypeInfo.IsValueType || Nullable.GetUnderlyingType(property.GetType()) != null;
                if (value != null || isReferenceTypeOrNullable)
                {
                    property.SetValue(subject, value);
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}

