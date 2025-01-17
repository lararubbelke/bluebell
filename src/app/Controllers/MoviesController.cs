﻿using Helium.DataAccessLayer;
using Helium.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Helium.Controllers
{
    /// <summary>
    /// Handle all of the /api/movies requests
    /// </summary>
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class MoviesController : Controller
    {
        private readonly ILogger _logger;
        private readonly IDAL _dal;
        private readonly Random _rand = new Random(DateTime.Now.Millisecond);

        /// <summary>
        ///  Constructor
        /// </summary>
        /// <param name="logger">log instance</param>
        /// <param name="dal">data access layer instance</param>
        public MoviesController(ILogger<MoviesController> logger, IDAL dal)
        {
            _logger = logger;
            _dal = dal;
        }

        /// <summary>
        /// </summary>
        /// <remarks>Returns a JSON array of Movie objects</remarks>
        /// <param name="q">(optional) The term used to search by movie title (rings)</param>
        /// <param name="genre">(optional) Movies of a genre (Action)</param>
        /// <param name="year">(optional) Get movies by year (2005)</param>
        /// <param name="rating">(optional) Get movies with a rating >= rating (8.5)</param>
        /// <param name="topRated">(optional) Get top rated movies (true)</param>
        /// <param name="actorId">(optional) Get movies by Actor Id (nm0000704)</param>
        /// <param name="pageNumber">1 based page index</param>
        /// <param name="pageSize">page size (1000 max)</param>
        /// <response code="200">JSON array of Movie objects or empty array if not found</response>
        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Movie[]), 200)]
        public async Task<IActionResult> GetMoviesAsync([FromQuery]string q = "", [FromQuery] string genre = "", [FromQuery] int year = 0, [FromQuery] double rating = 0, [FromQuery] bool topRated = false, [FromQuery] string actorId = "", [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = Constants.DefaultPageSize)
        {
            string method = GetMethod(q, genre, year, rating, topRated, actorId, pageNumber, pageSize);

            _logger.LogInformation(method);

            try
            {
                if (pageSize < 1)
                {
                    pageSize = Constants.DefaultPageSize;
                }
                else if (pageSize > Constants.MaxPageSize)
                {
                    pageSize = Constants.MaxPageSize;
                }

                pageNumber--;

                if (pageNumber < 0)
                {
                    pageNumber = 0;
                }

                return Ok(await _dal.GetMoviesByQueryAsync(q, genre, year, rating, topRated, actorId, pageNumber * pageSize, pageSize));
            }

            catch (CosmosException ce)
            {
                // log and return Cosmos status code
                _logger.LogError($"CosmosException:{method}:{ce.StatusCode}:{ce.ActivityId}:{ce.Message}\n{ce}");

                return new ObjectResult(Constants.MoviesControllerException)
                {
                    StatusCode = (int)ce.StatusCode
                };
            }

            catch (System.AggregateException age)
            {
                var root = age.GetBaseException();

                if (root == null)
                {
                    root = age;
                }

                // log and return 500
                _logger.LogError($"AggregateException|{method}|{root.GetType()}|{root.Message}|{root.Source}|{root.TargetSite}");

                return new ObjectResult(Constants.MoviesControllerException)
                {
                    StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                };
            }

            catch (Exception ex)
            {
                _logger.LogError($"{method}\n{ex}");

                return new ObjectResult(Constants.MoviesControllerException)
                {
                    StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        /// <summary>
        /// </summary>
        /// <remarks>Returns a single JSON Movie by movieId</remarks>
        /// <param name="movieId">The movieId</param>
        /// <response code="404">movieId not found</response>
        [HttpGet("{movieId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Movie), 200)]
        [ProducesResponseType(typeof(void), 404)]
        public async System.Threading.Tasks.Task<IActionResult> GetMovieByIdAsync(string movieId)
        {
            _logger.LogInformation($"GetMovieByIdAsync {movieId}");

            try
            {
                // get movie by movieId
                // CosmosDB API will throw an exception on a bad movieId
                Movie m = await _dal.GetMovieAsync(movieId);

                return Ok(m);
            }

            // movieId isn't well formed
            catch (ArgumentException)
            {
                _logger.LogInformation($"NotFound:GetMovieByIdAsync:{movieId}");

                // return a 404
                return NotFound();
            }

            catch (CosmosException ce)
            {
                // CosmosDB API will throw an exception on an movieId not found
                if (ce.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation($"NotFound:GetMovieByIdAsync:{movieId}");

                    // return a 404
                    return NotFound();
                }
                else
                {
                    // log and return Cosmos status code
                    _logger.LogError($"CosmosException:MovieByIdAsync:{ce.StatusCode}:{ce.ActivityId}:{ce.Message}\n{ce}");

                    return new ObjectResult(Constants.MoviesControllerException)
                    {
                        StatusCode = (int)ce.StatusCode
                    };
                }
            }

            catch (System.AggregateException age)
            {
                var root = age.GetBaseException();

                if (root == null)
                {
                    root = age;
                }

                // log and return 500
                _logger.LogError($"AggregateException|MovieByIdAsync|{root.GetType()}|{root.Message}|{root.Source}|{root.TargetSite}");

                return new ObjectResult(Constants.MoviesControllerException)
                {
                    StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                };
            }

            catch (Exception e)
            {
                // log and return 500
                _logger.LogError($"Exception:GetActorByIdAsync:{e.Message}\n{e}");

                return new ObjectResult(Constants.MoviesControllerException)
                {
                    StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        /// <summary>
        /// Add parameters to the method name if specified in the query string
        /// </summary>
        /// <param name="q"></param>
        /// <param name="genre"></param>
        /// <param name="year"></param>
        /// <param name="rating"></param>
        /// <param name="topRated"></param>
        /// <param name="actorId"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        private string GetMethod(string q, string genre, int year, double rating, bool topRated, string actorId, int pageNumber, int pageSize)
        {
            string method = "GetMovies";

            if (HttpContext != null && HttpContext.Request != null && HttpContext.Request.Query != null)
            {
                // add the query parameters to the method name if exists
                if (HttpContext.Request.Query.ContainsKey("q"))
                {
                    method = string.Format($"{method}:q:{q}");
                }

                if (HttpContext.Request.Query.ContainsKey("genre"))
                {
                    method = string.Format($"{method}:genre:{genre}");
                }

                if (HttpContext.Request.Query.ContainsKey("year"))
                {
                    method = string.Format($"{method}:year:{year}");
                }

                if (HttpContext.Request.Query.ContainsKey("rating"))
                {
                    method = string.Format($"{method}:rating:{rating}");
                }

                if (HttpContext.Request.Query.ContainsKey("topRated") && topRated)
                {
                    method = string.Format($"{method}:topRated:true");
                }

                if (HttpContext.Request.Query.ContainsKey("actorId"))
                {
                    method = string.Format($"{method}:actorId:{actorId}");
                }

                if (HttpContext.Request.Query.ContainsKey("pageNumber"))
                {
                    method = string.Format($"{method}:pageNumber:{pageNumber}");
                }

                if (HttpContext.Request.Query.ContainsKey("pageSize"))
                {
                    method = string.Format($"{method}:pageSize:{pageSize}");
                }
            }

            return method;
        }
    }
}
