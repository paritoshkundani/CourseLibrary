using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CourseLibrary.API.Models
{
    public class CourseForUpdateDto : CourseForManipulationDto
    {
        // override with Required attribute, base Maxlength will also apply
        [Required(ErrorMessage = "You should fill out a description.")]
        public override string Description { get => base.Description; set => base.Description = value; }
    }
}
