// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Internal;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.ViewFeatures.Internal
{
    public class SaveTempDataPropertyFilterTest
    {
        [Fact]
        public void SaveTempDataPropertyFilter_PopulatesTempDataWithValuesFromControllerProperty()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            {
                ["TempDataProperty-Test"] = "FirstValue"
            };            

            var filter = BuildSaveTempDataPropertyFilter(httpContext, tempData);

            var controller = new TestController();
            var controllerType = controller.GetType().GetTypeInfo();

            var propertyHelper1 = new PropertyHelper(controllerType.GetProperty(nameof(TestController.Test)));
            var propertyHelper2 = new PropertyHelper(controllerType.GetProperty(nameof(TestController.Test2)));
            var propertyHelpers = new List<PropertyHelper>
            {
                propertyHelper1,
                propertyHelper2,
            };

            filter.PropertyHelpers = propertyHelpers;
            var context = new ActionExecutingContext(
                new ActionContext
                {
                    HttpContext = httpContext,
                    RouteData = new RouteData(),
                    ActionDescriptor = new ActionDescriptor(),
                },
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                controller);

            // Act
            filter.OnActionExecuting(context);
            controller.Test = "SecondValue";
            filter.OnTempDataSaving(tempData);

            // Assert
            Assert.Equal("SecondValue", controller.Test);
            Assert.Equal("SecondValue", tempData["TempDataProperty-Test"]);
            Assert.Equal(0, controller.Test2);
        }

        [Fact]
        public void SaveTempDataPropertyFilter_ReadsTempDataFromTempDataDictionary()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();

            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            {
                ["TempDataProperty-Test"] = "FirstValue"
            };

            var filter = BuildSaveTempDataPropertyFilter(httpContext, tempData: tempData);
            var controller = new TestController();
            var controllerType = controller.GetType().GetTypeInfo();

            var propertyHelper1 = new PropertyHelper(controllerType.GetProperty(nameof(TestController.Test)));
            var propertyHelper2 = new PropertyHelper(controllerType.GetProperty(nameof(TestController.Test2)));
            var propertyHelpers = new List<PropertyHelper>
            {
                propertyHelper1,
                propertyHelper2,
            };

            filter.PropertyHelpers = propertyHelpers;

            var context = new ActionExecutingContext(
                new ActionContext
                {
                    HttpContext = httpContext,
                    RouteData = new RouteData(),
                    ActionDescriptor = new ActionDescriptor(),
                },
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                controller);

            // Act
            filter.OnActionExecuting(context);
            filter.OnTempDataSaving(tempData);

            // Assert
            Assert.Equal("FirstValue", controller.Test);
            Assert.Equal(0, controller.Test2);
        }

        [Fact]
        public void LoadAndTrackChanges_SetsPropertyValue()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();

            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            tempData["TempDataProperty-Test"] = "Value";
            tempData.Save();

            var controller = new TestControllerStrings()
            {
                TempData = tempData,
            };

            var provider = BuildSaveTempDataPropertyFilter(httpContext, tempData: tempData);



            // Act
            provider.LoadAndTrackChanges(controller, controller.TempData);

            // Assert
            Assert.Equal("Value", controller.Test);
            Assert.Null(controller.Test2);
        }

        [Fact]
        public void LoadAndTrackChanges_ThrowsInvalidOperationException_PrivateSetter()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            {
                {"TempDataProperty-Test", "Value" }
            };

            var provider = BuildSaveTempDataPropertyFilter(httpContext, tempData: tempData);
            
            tempData.Save();

            var controller = new TestController_PrivateSet()
            {
                TempData = tempData,
            };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.LoadAndTrackChanges(controller, controller.TempData));

            Assert.Equal("TempData properties must have a public getter and setter.", exception.Message);
        }

        [Fact]
        public void LoadAndTrackChanges_ThrowsInvalidOperationException_NonPrimitiveType()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            {
                { "TempDataProperty-Test", new object() }
            };
            var provider = BuildSaveTempDataPropertyFilter(httpContext, tempData: tempData);

            tempData.Save();

            var controller = new TestController_NonPrimitiveType()
            {
                TempData = tempData,
            };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.LoadAndTrackChanges(controller, controller.TempData));

            Assert.Equal("TempData properties must be declared as primitive types or string only.", exception.Message);
        }

        private SaveTempDataPropertyFilter BuildSaveTempDataPropertyFilter(
            HttpContext httpContext,
            TempDataDictionary tempData)
        {
            var factory = new Mock<ITempDataDictionaryFactory>();
            factory.Setup(f => f.GetTempData(httpContext))
                .Returns(tempData);

            return new SaveTempDataPropertyFilter(factory.Object);
        }

        public class TestController_NonPrimitiveType : Controller
        {
            [TempData]
            public object Test { get; set; }
        }

        public class TestController_PrivateSet : Controller
        {
            [TempData]
            public string Test { get; private set; }
        }

        public class TestControllerStrings : Controller
        {
            [TempData]
            public string Test { get; set; }

            [TempData]
            public string Test2 { get; set; }
        }

        public class TestController : Controller
        {
            [TempData]
            public string Test { get; set; }

            [TempData]
            public int Test2 { get; set; }
        }
    }
}
