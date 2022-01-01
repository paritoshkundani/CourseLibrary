using AutoMapper;
using CourseLibrary.API.ActionConstraints;
using CourseLibrary.API.Entities;
using CourseLibrary.API.Helpers;
using CourseLibrary.API.Models;
using CourseLibrary.API.ResourceParameters;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
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

            /*
             * removed as now using new HATEOAS way
            // is there a previous page
            var previousPageLink = authorsFromRepo.HasPrevious ?
                CreateAuthorsResouceUri(authorResourceParameters, ResourceUriType.PreviousPage) : null;

            // is there a next page
            var nextPageLink = authorsFromRepo.HasNext ?
                CreateAuthorsResouceUri(authorResourceParameters, ResourceUriType.NextPage) : null;
            */

            // create metadata
            var paginationMetadata = new
            {
                totalCount = authorsFromRepo.TotalCount,
                pageSize = authorsFromRepo.PageSize,
                currentPage = authorsFromRepo.CurrentPage,
                totalPages = authorsFromRepo.TotalPages,

               // previousPageLink = previousPageLink,  // since the name is the same, VS will allow just previousPageLink without previousPageLink = previousPageLink
               // nextPageLink = nextPageLink // same as above, that is why VS is showing a tooltip
            };

            // create a custom header response
            Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(paginationMetadata));

            var links = CreateLinksForAuthors(authorResourceParameters, authorsFromRepo.HasNext, authorsFromRepo.HasPrevious);

            var shapedAuthors = _mapper.Map<IEnumerable<AuthorDto>>(authorsFromRepo)
                .ShapeData(authorResourceParameters.Fields);

            var shapedAuthorsWithLinks = shapedAuthors.Select(author =>
            {
                var authorAsDictionary = author as IDictionary<string, object>;
                var authorLinks = CreateLinksForAuthor((Guid)authorAsDictionary["Id"], null);
                authorAsDictionary.Add("links", authorLinks);
                return authorAsDictionary;
            });

            var linkedCollectionResource = new
            {
                value = shapedAuthorsWithLinks,
                links
            };

            return Ok(linkedCollectionResource);

            //return Ok(_mapper.Map<IEnumerable<AuthorDto>>(authorsFromRepo)
            //    .ShapeData(authorResourceParameters.Fields));
        }

        // :guid will force it to only take guid, incase there is another similar with int for example
        // mediaType we get to check the Accept header and return different results based on it
        // Produce tells what API can send response back as, we can do it globally like we did in 
        // startup for application/vnd.marvin.hateoas+json but here wanted to show a Action specific way as well
        // we can apply Produces at Controller level as well
        [HttpGet("{authorId:guid}", Name = "GetAuthor")]
        [Produces("application/json",
            "application/vnd.marvin.hateoas+json",
            "application/vnd.marvin.author.full+json",
            "application/vnd.marvin.author.full.hateoas+json",
            "application/vnd.marvin.author.friendly+json",
            "application/vnd.marvin.author.friendly.hateoas+json")]
        public IActionResult GetAuthor(Guid authorId, string fields, [FromHeader(Name = "Accept")] string mediaType)
        {
            // make sure media type if parsable to confirm format is ok, as here we will also
            // return a vedor specific sometimes
            if (!MediaTypeHeaderValue.TryParse(mediaType, out MediaTypeHeaderValue parseMediaType))
            {
                return BadRequest();
            }

            if (!_propertyCheckerService.TypeHasProperties<AuthorDto>(fields))
            {
                return BadRequest();
            }

            var authorFromRepo = _courseLibraryRepository.GetAuthor(authorId);

            if (authorFromRepo == null)
            {
                return NotFound();
            }

            // do we have hateoas in accept header
            var includeLinks = parseMediaType.SubTypeWithoutSuffix.EndsWith("hateoas", StringComparison.InvariantCultureIgnoreCase);

            IEnumerable<LinkDto> links = new List<LinkDto>();

            if (includeLinks)
            {
                links = CreateLinksForAuthor(authorId, fields);
            }

            // checking to see if the :full is in accept header or not
            var primaryMediaType = includeLinks ?
                parseMediaType.SubTypeWithoutSuffix
                .Substring(0, parseMediaType.SubTypeWithoutSuffix.Length - 8)
                : parseMediaType.SubTypeWithoutSuffix;

            // full author
            if (primaryMediaType == "vnd.marvin.author.full")
            {
                var fullResourceToReturn = _mapper.Map<AuthorFullDto>(authorFromRepo)
                    .ShapeData(fields) as IDictionary<string, object>;

                if (includeLinks)
                {
                    fullResourceToReturn.Add("links", links);
                }

                return Ok(fullResourceToReturn);
            }

            // friendly author
            var friendlyResourceToReturn = _mapper.Map<AuthorDto>(authorFromRepo)
                    .ShapeData(fields) as IDictionary<string, object>;

            if (includeLinks)
            {
                friendlyResourceToReturn.Add("links", links);
            }

            return Ok(friendlyResourceToReturn);
        }

        // RequestHeaderMatchesMediaType is a custom attribute made to ROUTE this method if
        // Content-Type is application/vnd.marvin.authorforcreationwithdateofdeath+json -> for route to allow
        // Consumes attribute allows it to be called if that particular input type is used -> if route allows we 
        // also only want this as input type
        // NOTE: order does matter between this and the next one CreateAuthor, 2nd one accepts application/json
        // which is more generic, so to make sure this one is triggered without a 404 we need to put it before
        // next one, order matters for this one
        [HttpPost(Name = "CreateAuthorWithDateOfDeath")]
        [RequestHeaderMatchesMediaType("Content-Type",
            "application/vnd.marvin.authorforcreationwithdateofdeath+json")]
        [Consumes("application/vnd.marvin.authorforcreationwithdateofdeath+json")]
        public ActionResult<AuthorDto> CreateAuthorWithDateOfDeath(AuthorForCreationWithDateOfDeathDto author)
        {
            var authorEntity = _mapper.Map<Entities.Author>(author);
            _courseLibraryRepository.AddAuthor(authorEntity);
            _courseLibraryRepository.Save();

            var authorToReturn = _mapper.Map<Models.AuthorDto>(authorEntity);

            var links = CreateLinksForAuthor(authorToReturn.Id, null);

            // make an ExpandoObject to add the links to the existing response
            var linkedResourceReturn = authorToReturn.ShapeData(null)
                as IDictionary<string, object>;
            linkedResourceReturn.Add("links", links);

            // return 201 created response
            return CreatedAtRoute("GetAuthor", new { authorId = linkedResourceReturn["Id"] }, linkedResourceReturn);
        }

        // since we used [ApiController] it includes the bad request check by default:
        // if (author == null)
        // {
        //  return BadRequest();
        // }
        // RequestHeaderMatchesMediaType is a custom attribute made to ROUTE this method if
        // Content-Type is either application/json or application/vnd.marvin.authorforcreation+json -> for route to allow
        // Consumes attribute allows it to be called if those particular input types are used -> if route allow we
        // also want these as input types
        [HttpPost(Name = "CreateAuthor")]
        [RequestHeaderMatchesMediaType("Content-Type",
            "application/json",
            "application/vnd.marvin.authorforcreation+json")]
        [Consumes("application/json",
            "application/vnd.marvin.authorforcreation+json")]
        public ActionResult<AuthorDto> CreateAuthor(AuthorForCreationDto author)
        {
            var authorEntity = _mapper.Map<Entities.Author>(author);
            _courseLibraryRepository.AddAuthor(authorEntity);
            _courseLibraryRepository.Save();

            var authorToReturn = _mapper.Map<Models.AuthorDto>(authorEntity);

            var links = CreateLinksForAuthor(authorToReturn.Id, null);

            // make an ExpandoObject to add the links to the existing response
            var linkedResourceReturn = authorToReturn.ShapeData(null)
                as IDictionary<string, object>;
            linkedResourceReturn.Add("links", links);

            // return 201 created response
            return CreatedAtRoute("GetAuthor", new { authorId = linkedResourceReturn["Id"] }, linkedResourceReturn);
        }


        // will let the consumer know if they can get the resouce, post to it, delete it and so on
        [HttpOptions]
        public IActionResult GetAuthorOptions()
        {
            Response.Headers.Add("Allow", "GET,OPTIONS,POST");
            return Ok();
        }

        [HttpDelete("{authorId}", Name = "DeleteAuthor")]
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
                case ResourceUriType.Current:
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

        // create HATEOAS links (providing links with data returned, allowing consumer to know whatelse they
        // can do with Authors data
        private IEnumerable<LinkDto> CreateLinksForAuthor(Guid authorId, string fields)
        {
            var links = new List<LinkDto>();

            if (string.IsNullOrWhiteSpace(fields))
            {
                links.Add(
                    new LinkDto(Url.Link("GetAuthor", new { authorId }),
                    "self",
                    "GET"));
            }
            else
            {
                links.Add(
                    new LinkDto(Url.Link("GetAuthor", new { authorId, fields }),
                    "self",
                    "GET"));
            }

            links.Add(
                new LinkDto(Url.Link("DeleteAuthor", new { authorId }),
                "delete_author",
                "DELETE"));

            links.Add(
                new LinkDto(Url.Link("CreateCourseForAuthor", new { authorId }),
                "create_course_for_author",
                "POST"));

            links.Add(
                new LinkDto(Url.Link("GetCoursesForAuthor", new { authorId }),
                "courses",
                "GET"));

            return links;
        }

        private IEnumerable<LinkDto> CreateLinksForAuthors(AuthorResourceParameters authorResourceParameters,
            bool hasNext, bool hasPrevious)
        {
            var links = new List<LinkDto>();

            // self
            links.Add(new LinkDto(CreateAuthorsResouceUri(authorResourceParameters, ResourceUriType.Current)
                , "self", "GET"));

            if (hasNext)
            {
                links.Add(new LinkDto(CreateAuthorsResouceUri(authorResourceParameters, ResourceUriType.NextPage)
                    , "nextPage", "GET"));
            }

            if (hasPrevious)
            {
                links.Add(new LinkDto(CreateAuthorsResouceUri(authorResourceParameters, ResourceUriType.PreviousPage)
                    , "previousPage", "GET"));
            }

            return links;
        }
    }
}
