using AutoMapper;
using CourseLibrary.API.Entities;
using CourseLibrary.API.Helpers;
using CourseLibrary.API.Models;
using CourseLibrary.API.ResourceParameters;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CourseLibrary.API.Controllers
{
    [ApiController]
    [Route("api/authors")]
    public class AuthorsController : ControllerBase
    {
        private readonly ICourseLibraryRepository _courseLibraryRepository;
        private readonly IMapper _mapper;
        private readonly IPropertyCheckerService _propertyCheckerService;

        private IPropertyMappingService _propertyMappingService { get; }

        public AuthorsController(ICourseLibraryRepository courseLibraryRepository, IMapper mapper, 
            IPropertyMappingService propertyMappingService,
            IPropertyCheckerService propertyCheckerService)
        {
            _courseLibraryRepository = courseLibraryRepository ??
                throw new ArgumentNullException(nameof(courseLibraryRepository));
            this._mapper = mapper ??
                throw new ArgumentNullException(nameof(mapper));
            _propertyMappingService = propertyMappingService ??
                throw new ArgumentNullException(nameof(propertyMappingService));
            _propertyCheckerService = propertyCheckerService ??
                throw new ArgumentNullException(nameof(propertyCheckerService));
            ;
        }

        // httphead is similar to httpget, but does not include the body
        // can be used to verify something route is still there, look for 200 status without
        // checking for body, returns just response headers
        // because we used ControllerBase it is able to infer mainCategory and searchQuery are from QueryString (before we changed it to AuthorResourceParameters)
        // once we made it to return an ExpandoObject, changed return to IActionResult rather than ActionResult<T>
        [HttpGet(Name = "GetAuthors")]
        [HttpHead]
        public IActionResult GetAuthors([FromQuery] AuthorResourceParameters authorResourceParameters)
        {
            // if property being passed in does not exist in the mapping
            if (!_propertyMappingService.ValidMappingExistsFor<AuthorDto, Author>(authorResourceParameters.OrderBy))
            {
                return BadRequest();
            }

            if (!_propertyCheckerService.TypeHasProperties<AuthorDto>(authorResourceParameters.Fields))
            {
                return BadRequest();
            }

            var authorsFromRepo = _courseLibraryRepository.GetAuthors(authorResourceParameters);

            // is there a previous page
            var previousPageLink = authorsFromRepo.HasPrevious ?
                CreateAuthorsResouceUri(authorResourceParameters, ResourceUriType.PreviousPage) : null;

            // is there a next page
            var nextPageLink = authorsFromRepo.HasNext ?
                CreateAuthorsResouceUri(authorResourceParameters, ResourceUriType.NextPage) : null;

            // create metadata
            var paginationMetadata = new
            {
                totalCount = authorsFromRepo.TotalCount,
                pageSize = authorsFromRepo.PageSize,
                currentPage = authorsFromRepo.CurrentPage,
                totalPages = authorsFromRepo.TotalPages,
                previousPageLink = previousPageLink,  // since the name is the same, VS will allow just previousPageLink without previousPageLink = previousPageLink
                nextPageLink = nextPageLink // same as above, that is why VS is showing a tooltip
            };

            // create a custom header response
            Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(paginationMetadata));

            return Ok(_mapper.Map<IEnumerable<AuthorDto>>(authorsFromRepo)
                .ShapeData(authorResourceParameters.Fields));
        }

        // :guid will force it to only take guid, incase there is another similar with int for example
        [HttpGet("{authorId:guid}", Name = "GetAuthor")]
        public IActionResult GetAuthor(Guid authorId, string fields)
        {
            if (!_propertyCheckerService.TypeHasProperties<AuthorDto>(fields))
            {
                return BadRequest();
            }

            var authorFromRepo = _courseLibraryRepository.GetAuthor(authorId);

            if (authorFromRepo == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<AuthorDto>(authorFromRepo).ShapeData(fields));
        }

        // since we used [ApiController] it includes the bad request check by default:
        // if (author == null)
        // {
        //  return BadRequest();
        // }
        [HttpPost]
        public ActionResult<AuthorDto> CreateAuthor(AuthorForCreationDto author)
        {
            var authorEntity = _mapper.Map<Entities.Author>(author);
            _courseLibraryRepository.AddAuthor(authorEntity);
            _courseLibraryRepository.Save();

            var authorToReturn = _mapper.Map<Models.AuthorDto>(authorEntity);
            // return 201 created response
            return CreatedAtRoute("GetAuthor", new { authorId = authorEntity.Id }, authorToReturn);
        }

        // will let the consumer know if they can get the resouce, post to it, delete it and so on
        [HttpOptions]
        public IActionResult GetAuthorOptions()
        {
            Response.Headers.Add("Allow", "GET,OPTIONS,POST");
            return Ok();
        }

        [HttpDelete("{authorId}")]
        public ActionResult DeleteAuthor(Guid authorId)
        {
            var authorFromRepo = _courseLibraryRepository.GetAuthor(authorId);

            if (authorFromRepo == null)
            {
                return NotFound();
            }

            // cascade delete is on my default in EF, so deleting an Author will also delete
            // related Courses
            _courseLibraryRepository.DeleteAuthor(authorFromRepo);
            _courseLibraryRepository.Save();

            // 204
            return NoContent();
        }

        // used to generate the previous/next links to return to client
        private string CreateAuthorsResouceUri(AuthorResourceParameters authorResourceParameters, ResourceUriType type)
        {
            switch (type)
            {
                case ResourceUriType.PreviousPage:
                    return Url.Link("GetAuthors",
                        new
                        {
                            fields = authorResourceParameters.Fields,
                            orderBy = authorResourceParameters.OrderBy,
                            pageNumber = authorResourceParameters.PageNumber - 1,
                            pageSize = authorResourceParameters.PageSize,
                            mainCategory = authorResourceParameters.MainCategory,
                            searchQuery = authorResourceParameters.SearchQuery
                        });
                case ResourceUriType.NextPage:
                    return Url.Link("GetAuthors",
                        new
                        {
                            fields = authorResourceParameters.Fields,
                            orderBy = authorResourceParameters.OrderBy,
                            pageNumber = authorResourceParameters.PageNumber + 1,
                            pageSize = authorResourceParameters.PageSize,
                            mainCategory = authorResourceParameters.MainCategory,
                            searchQuery = authorResourceParameters.SearchQuery
                        });
                default:
                    return Url.Link("GetAuthors",
                        new
                        {
                            fields = authorResourceParameters.Fields,
                            orderBy = authorResourceParameters.OrderBy,
                            pageNumber = authorResourceParameters.PageNumber,
                            pageSize = authorResourceParameters.PageSize,
                            mainCategory = authorResourceParameters.MainCategory,
                            searchQuery = authorResourceParameters.SearchQuery
                        });
            }
        }
    }
}
