using AutoMapper;
using CourseLibrary.API.Models;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CourseLibrary.API.Controllers
{
    [ApiController]
    [Route("api/authors/{authorId}/courses")]
    public class CoursesController : ControllerBase
    {
        private readonly ICourseLibraryRepository _courseLibraryRepository;
        private readonly IMapper _mapper;

        public CoursesController(ICourseLibraryRepository courseLibraryRepository, IMapper mapper)
        {
            _courseLibraryRepository = courseLibraryRepository ??
                throw new ArgumentNullException(nameof(courseLibraryRepository));
            this._mapper = mapper ??
                throw new ArgumentNullException(nameof(mapper));
        }

        [HttpGet(Name = "GetCoursesForAuthor")]
        public ActionResult<IEnumerable<CourseDto>> GetCoursesForAuthor(Guid authorId)
        {
            if (!_courseLibraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var coursesForAuthorFromRepo = _courseLibraryRepository.GetCourses(authorId);
            return Ok(_mapper.Map<IEnumerable<CourseDto>>(coursesForAuthorFromRepo));
        }

        [HttpGet("{courseId}", Name = "GetCourseForAuthor")]
        public ActionResult<CourseDto> GetCourseForAuthor(Guid authorId, Guid courseId)
        {
            if (!_courseLibraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var coursesForAuthorFromRepo = _courseLibraryRepository.GetCourse(authorId, courseId);

            if (coursesForAuthorFromRepo == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<CourseDto>(coursesForAuthorFromRepo));
        }

        [HttpPost(Name = "CreateCourseForAuthor")]
        public ActionResult<CourseDto> CreateCourseForAuthor(Guid authorId, CourseForCreationDto course)
        {
            if (!_courseLibraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var courseEntity = _mapper.Map<Entities.Course>(course);
            _courseLibraryRepository.AddCourse(authorId, courseEntity);
            _courseLibraryRepository.Save();

            var courseToReturn = _mapper.Map<CourseDto>(courseEntity);
            // return 201 created response
            return CreatedAtRoute(
                "GetCourseForAuthor",
                new { authorId = authorId, courseId = courseEntity.Id },
                courseToReturn
            );
        }

        // authorId is already coming from the Controller level route, so we just need courseId here
        // using IActionResult as we return either 201 with some data or 204 no content
        [HttpPut("{courseId}")]
        public IActionResult UpdateCourseForAuthor(Guid authorId, Guid courseId, CourseForUpdateDto course)
        {
            if (!_courseLibraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var coursesForAuthorFromRepo = _courseLibraryRepository.GetCourse(authorId, courseId);

            if (coursesForAuthorFromRepo == null)
            {
                // going to Upsert (use PUT to create if it does not exist)
                var courseToAdd = _mapper.Map<Entities.Course>(course);
                courseToAdd.Id = courseId;

                _courseLibraryRepository.AddCourse(authorId, courseToAdd);

                _courseLibraryRepository.Save();

                var courseToReturn = _mapper.Map<CourseDto>(courseToAdd);

                // 201
                return CreatedAtRoute("GetCourseForAuthor",
                    new { authorId = authorId, courseId = courseToReturn.Id },
                    courseToReturn);
            }

            // map CourseForUpdateDto to Entity
            _mapper.Map(course, coursesForAuthorFromRepo);

            // once the map above is done the Entity is marked Modified by EF, the UpdateCourse 
            // method in the Repo actually does nothing as EF already knows the entity has changed
            _courseLibraryRepository.UpdateCourse(coursesForAuthorFromRepo);

            _courseLibraryRepository.Save();

            // 204
            return NoContent();
        }

        [HttpPatch("{courseId}")]
        public ActionResult PartiallyUpdateCourseForAuthor(Guid authorId, Guid courseId, JsonPatchDocument<CourseForUpdateDto> patchDocument)
        {
            if (!_courseLibraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var coursesForAuthorFromRepo = _courseLibraryRepository.GetCourse(authorId, courseId);

            if (coursesForAuthorFromRepo == null)
            {
                // upserting, so lets make a courseforupdate
                var courseDto = new CourseForUpdateDto();
                patchDocument.ApplyTo(courseDto, ModelState);

                if (!TryValidateModel(courseDto))
                {
                    return ValidationProblem(ModelState);
                }

                var courseToAdd = _mapper.Map<Entities.Course>(courseDto);
                courseToAdd.Id = courseId;

                _courseLibraryRepository.AddCourse(authorId, courseToAdd);
                _courseLibraryRepository.Save();

                var courseToReturn = _mapper.Map<CourseDto>(courseToAdd);

                // 201
                return CreatedAtRoute("GetCourseForAuthor",
                    new { authorId = authorId, courseId = courseToReturn.Id },
                    courseToReturn);
            }

            var courseToPatch = _mapper.Map<CourseForUpdateDto>(coursesForAuthorFromRepo);
            // apply the patchdocument
            // passing ModelState so any errors in courseToPatch (for example removing a property that does not exist) will cause ModelState to be invalid
            patchDocument.ApplyTo(courseToPatch, ModelState);

            // add validation since we are working JsonPatchDocument, CourseForUpdateDto rules (like required)
            // won't trigger, we need to check it manually, otherwise we can remove description for example
            // which for update should not be allowed as that's required
            if (!TryValidateModel(courseToPatch)) // this will trigger error checks on the model, which will be in ModelState
            {
                return ValidationProblem(ModelState);
            }

            _mapper.Map(courseToPatch, coursesForAuthorFromRepo);

            _courseLibraryRepository.UpdateCourse(coursesForAuthorFromRepo);

            _courseLibraryRepository.Save();

            // 204
            return NoContent();
        }

        [HttpDelete("{courseId}")]
        public ActionResult DeleteCourseForAuthor(Guid authorId, Guid courseId)
        {
            if (!_courseLibraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var coursesForAuthorFromRepo = _courseLibraryRepository.GetCourse(authorId, courseId);

            if (coursesForAuthorFromRepo == null)
            {
                return NotFound();
            }

            _courseLibraryRepository.DeleteCourse(coursesForAuthorFromRepo);
            _courseLibraryRepository.Save();

            // 204
            return NoContent();
        }

        // did all the below to get a 422 status code rather than 400 when patch validation above .ApplyTo(courseToPatch, ModelState) fails
        // otherwise without this will be fine as well
        public override ActionResult ValidationProblem([ActionResultObjectValue] ModelStateDictionary modelStateDictionary)
        {
            var options = HttpContext.RequestServices.GetRequiredService<IOptions<ApiBehaviorOptions>>();
            return (ActionResult)options.Value.InvalidModelStateResponseFactory(ControllerContext);
        }
    }
}
