using CourseLibrary.API.CustomAttributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CourseLibrary.API.Models
{
    /*
     * IValidatableObject will trigger validation BEFORE the attribute level ones, so if that errors
     * user will not see the others, such as Required
     * 
     * Class level like CourseTitleMustBeDifferentFromDescriptionAttribute will run AFTER attribute ones pass
     */

    [CourseTitleMustBeDifferentFromDescriptionAttribute(ErrorMessage = "Title must be different from description")]
    public class CourseForCreationDto //: IValidatableObject
    {
        [Required(ErrorMessage = "You should fill out a title.")]
        [MaxLength(100, ErrorMessage = "The title shouldn't have more than 100 characters")]
        public string Title { get; set; }

        [MaxLength(1500, ErrorMessage = "The title shouldn't have more than 1500 characters")]
        public string Description { get; set; }


        // to use below implement IValidatableObject, we later moved it to class level with CourseTitleMustBeDifferentFromDescriptionAttribute
        // either approach will work
        // add custom rule to check to make sure Title is not same as Description
        //public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        //{
        //    if (Title == Description)
        //    {
        //        yield return new ValidationResult(
        //            "The provided description should be different from the title",
        //            new[] { "CourseForCreationDto" }
        //        );
        //    }
        //}
    }
}
