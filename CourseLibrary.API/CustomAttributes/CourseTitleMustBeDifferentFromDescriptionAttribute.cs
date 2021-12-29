using CourseLibrary.API.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CourseLibrary.API.CustomAttributes
{
    public class CourseTitleMustBeDifferentFromDescriptionAttribute : ValidationAttribute
    {
        // object is the object to validate (here Course)
        // validationContext will be used to access the object we're validating
        // here since we're checking aganist properties in the same class (CourseForManipulationDto) they are same
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var course = (CourseForManipulationDto)validationContext.ObjectInstance;

            if (course.Title == course.Description)
            {
                // ErrorMessage is setup in CourseForManipulationDto attribute setup, otherwise put a string here
                return new ValidationResult(
                    ErrorMessage,
                    new[] { "CourseForManipulationDto" }
                );
            }

            return ValidationResult.Success;
        }
    }
}
