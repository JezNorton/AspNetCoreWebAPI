using AutoMapper;
using CityInfo.API.Entities;
using CityInfo.API.Models;
using CityInfo.API.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace CityInfo.API.Controllers
{
    [Route("api/cities/{cityId}/pointsofinterest")]
    [ApiController]
    public class PointsOfInterestController : ControllerBase
    {
        private readonly ILogger<PointsOfInterestController> _logger;
        private readonly IMailService _localMailService;
        private readonly ICityInfoRepository _cityInfoRepository;
        private readonly IMapper _mapper;

        public PointsOfInterestController(ILogger<PointsOfInterestController> logger,
            IMailService localMailService,
            ICityInfoRepository cityInfoRepository,
            IMapper mapper
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _localMailService = localMailService ?? throw new ArgumentNullException(nameof(localMailService));
            _cityInfoRepository = cityInfoRepository ?? throw new ArgumentNullException(nameof(cityInfoRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }


        [HttpGet]
        public async Task<ActionResult<IEnumerable<PointOfInterestDto>>> PointsOfInterest(int cityId)
        {
            try
            {
                //throw new Exception("Exception sample");
                var cityExists = await _cityInfoRepository.CityExistsAsync(cityId);

                if (!cityExists)
                {
                    _logger.LogInformation($"City with id {cityId} wasn't found when trying to return PointsOfInterest");
                    return NotFound(cityId);
                }
                var pointsOfInterestEntities = await _cityInfoRepository.GetPointsOfInterestForCityAsync(cityId);
                return Ok(_mapper.Map<IEnumerable<PointOfInterestDto>>(pointsOfInterestEntities));
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"Exception when trying to locate points of interest for city {cityId}", ex);
                return StatusCode(500, "A problem occurred whilst handling your request.");
            }
        }
        [HttpGet("{pointOfInterestId}", Name = "GetPointOfInterest")]
        public async Task<ActionResult<PointOfInterestDto>> PointOfInterest(int cityId, int pointOfInterestId)
        {
            if (!await _cityInfoRepository.CityExistsAsync(cityId))
            {
                _logger.LogInformation($"City with id {cityId} wasn't found when trying to return PointOfInterest");
                return NotFound(cityId);
            }
            var pointOfInterestEntity = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
            if (pointOfInterestEntity == null)
            {
                _logger.LogInformation($"PointOfInterest with id {pointOfInterestId} wasn't found for city with id {cityId}");
                return NotFound(pointOfInterestId);
            }
            return Ok(_mapper.Map<PointOfInterestDto>(pointOfInterestEntity));
        }

        [HttpPost]
        public async Task<ActionResult<PointOfInterestDto>> CreatePointOfInterest(
            int cityId,
            PointOfInterestForCreationDto pointOfInterest)
        {
            ;
            if (!await _cityInfoRepository.CityExistsAsync(cityId)) return NotFound();
            var finalPointOfInterest = _mapper.Map<PointOfInterest>(pointOfInterest);
            await _cityInfoRepository.AddPointOfInterestForCityAsync(cityId, finalPointOfInterest);
            await _cityInfoRepository.SaveChangesAsync();
            var createdPointOfInterestToReturn = _mapper.Map<PointOfInterestDto>(finalPointOfInterest);
            return CreatedAtRoute("GetPointOfInterest",
                new { cityId = cityId, pointOfInterestId = finalPointOfInterest.Id },
                createdPointOfInterestToReturn);
        }

        [HttpPut("{pointOfInterestId}")]
        public async Task<ActionResult> UpdatePointOfInterest(
            int cityId,
            int pointOfInterestId,
            PointOfInterestForUpdateDto pointOfInterest)
        {
            if (!await _cityInfoRepository.CityExistsAsync(cityId)) return NotFound();
            var pointOfInterestFromStore = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
            if (pointOfInterestFromStore == null) return NotFound();

            _mapper.Map(pointOfInterest, pointOfInterestFromStore);
            await _cityInfoRepository.SaveChangesAsync();
            return NoContent();
        }
        [HttpPatch("{PointOfInterestId}")]
        public async Task<ActionResult> PartiallyUpdatePointOfInterest(
            int cityId,
            int pointOfInterestId,
            JsonPatchDocument<PointOfInterestForUpdateDto> patchDocument)
        {
            if (!await _cityInfoRepository.CityExistsAsync(cityId)) return NotFound();
            var pointOfInterestFromStore = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
            if (pointOfInterestFromStore == null) return NotFound();
            var pointOfInterestToPatch = _mapper.Map<PointOfInterestForUpdateDto>(pointOfInterestFromStore);
            patchDocument.ApplyTo(pointOfInterestToPatch, ModelState);
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!TryValidateModel(pointOfInterestToPatch)) return BadRequest(ModelState);
            _mapper.Map(pointOfInterestToPatch, pointOfInterestFromStore);
            await _cityInfoRepository.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{pointOfInterestId}")]
        public async Task<ActionResult> DeletePointOfInterest(
            int cityId,
            int pointOfInterestId)
        {
            var city = await _cityInfoRepository.GetCityAsync(cityId);
            if (city == null) return NotFound();
            var pointOfInterestFromStore = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
            if (pointOfInterestFromStore == null) return NotFound();
            _cityInfoRepository.DeletePointOfInterest(pointOfInterestFromStore);
            await _cityInfoRepository.SaveChangesAsync();
            _localMailService.Send(
                "Point of Interest Deleted",
                $"Point of interest {pointOfInterestFromStore.Name} with id {pointOfInterestFromStore.Id} " +
                $"was removed from city {city.Name} with id {city.Id}");
            return NoContent();
        }
    }
}
