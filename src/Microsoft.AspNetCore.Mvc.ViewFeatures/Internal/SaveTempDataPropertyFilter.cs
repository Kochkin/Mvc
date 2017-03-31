// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        /// Loads and tracks any changes to the properties of the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The properties of the subject are loaded and tracked. May be a <see cref="Controller"/>.</param>
        /// <param name="tempData">The <see cref="ITempDataDictionary"/>.</param>
        /// <returns></returns>
        public IDictionary<PropertyInfo, object> LoadAndTrackChanges(object subject, ITempDataDictionary tempData)
        {
            var properties = GetSubjectProperties(subject);
            var result = new Dictionary<PropertyInfo, object>();

            foreach (var property in properties)
            {
                var value = tempData[Prefix + property.Name];

                result[property] = value;

                // TODO: Clarify what behavior should be for null values here
                if (value != null && property.PropertyType.IsAssignableFrom(value.GetType()))
                {
                    property.SetValue(subject, value);
                }
            }

            return result;
        }

        private ConcurrentDictionary<Type, IEnumerable<PropertyInfo>> _subjectProperties =
            new ConcurrentDictionary<Type, IEnumerable<PropertyInfo>>();

        private IEnumerable<PropertyInfo> GetSubjectProperties(object subject)
        {
            return _subjectProperties.GetOrAdd(subject.GetType(), subjectType =>
            {
                var properties = subjectType.GetRuntimeProperties()
                    .Where(pi => pi.GetCustomAttribute<TempDataAttribute>() != null);

                if (properties.Any(pi => !(pi.SetMethod != null && pi.SetMethod.IsPublic && pi.GetMethod != null && pi.GetMethod.IsPublic)))
                {
                    throw new InvalidOperationException("TempData properties must have a public getter and setter.");
                }

                if (properties.Any(pi => !(pi.PropertyType.GetTypeInfo().IsPrimitive || pi.PropertyType == typeof(string))))
                {
                    throw new InvalidOperationException("TempData properties must be declared as primitive types or string only.");
                }

                return properties;
            });
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

            for (var i = 0; i < PropertyHelpers.Count; i++)
            {
                var property = PropertyHelpers[i].Property;
                var value = tempData[Prefix + property.Name];

                OriginalValues[property] = value;

                var propertyTypeInfo = property.PropertyType.GetTypeInfo();

                var isReferenceTypeOrNullable = !propertyTypeInfo.IsValueType || Nullable.GetUnderlyingType(property.GetType()) != null;
                if (value != null || isReferenceTypeOrNullable)
                {
                    property.SetValue(Subject, value);
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}

