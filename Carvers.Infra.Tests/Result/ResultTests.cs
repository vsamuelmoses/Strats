using System;
using System.Diagnostics.CodeAnalysis;
using Carvers.Infra.Result;
using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carvers.Infra.Tests.Result
{
    [TestClass]
    public class ResultTests
    {
        [TestMethod]
        public void Given_The_Value_Of_Type_T_Is_Valid_Can_Construct_A_Success_Result()
        {
            var innerValue = 23;
            var result = innerValue.ToSuccess();

            result.Value.IsSameOrEqualTo(innerValue);
            result.IsFailure.IsSameOrEqualTo(false);

            Action errorRetriever = () => { var error = result.Error; };
            errorRetriever.Should().Throw<Exception>();

        }

        [TestMethod]
        public void Given_An_Exception_Type_Can_Construct_A_Failure_Result_Of_Any_Type()
        {
            var innerError = new Exception("Fatal Error");
            var result = innerError.ToFailure<int>();

            result.Error.IsSameOrEqualTo(innerError);
            result.IsFailure.IsSameOrEqualTo(true);

            Action valueRetriever = () => { var val = result.Value; };
            valueRetriever.Should().Throw<Exception>();
        }

        [TestMethod]
        public void Given_A_Success_Result_Can_Invoke_OnSuccess_Action()
        {
            var value = 23;
            value
                .ToSuccess()
                .Match(
                    onSuccess: val => val.IsSameOrEqualTo(value), 
                    onFailure: error => Assert.Fail("Success Result should not execute failure action"));
        }


        [TestMethod]
        public void Given_A_Failure_Result_Can_Invoke_OnFailure_Action()
        {
            var error = new Exception("Error");
            error
                .ToFailure<int>()
                .Match(
                    onSuccess: val => Assert.Fail("Failure Result should not execute success action"),
                    onFailure: err => err.IsSameOrEqualTo(error));
        }
    }


    [TestClass]
    public partial class ResultExtensibilityTests
    {
        [TestClass]
        public class TryCastTests
        {
            [TestMethod]
            public void GIVEN_A_Null_Object_WHEN_Invoked_TryCast_THEN_A_Failure_Result_IsReturned()
            {
                object value = null;
                var result = value.TryCast<Action>();
                result.IsFailure.Should().BeTrue();
            }
        }
    }
}
