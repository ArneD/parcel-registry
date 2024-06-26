namespace ParcelRegistry.Api.BackOffice
{
    using System.Threading;
    using System.Threading.Tasks;
    using Abstractions.Requests;
    using Abstractions.SqsRequests;
    using Abstractions.Validation;
    using Be.Vlaanderen.Basisregisters.Auth.AcmIdm;
    using Be.Vlaanderen.Basisregisters.AggregateSource;
    using Be.Vlaanderen.Basisregisters.Api.ETag;
    using Be.Vlaanderen.Basisregisters.Api.Exceptions;
    using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
    using FluentValidation;
    using Infrastructure;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Parcel;
    using Swashbuckle.AspNetCore.Filters;
    using Validators;

    public partial class ParcelController
    {
        /// <summary>
        /// Koppel adres aan perceel.
        /// </summary>
        /// <param name="validator"></param>
        /// <param name="parcelExistsValidator"></param>
        /// <param name="ifMatchHeaderValidator"></param>
        /// <param name="caPaKey"></param>
        /// <param name="request"></param>
        /// <param name="ifMatchHeaderValue"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost("{caPaKey}/acties/adreskoppelen")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [SwaggerResponseHeader(StatusCodes.Status202Accepted, "location", "string", "De URL van het aangemaakte ticket.")]
        [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestResponseExamples))]
        [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(InternalServerErrorResponseExamples))]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = PolicyNames.Adres.DecentraleBijwerker)]
        public async Task<IActionResult> AttachAddress(
            [FromServices] IValidator<AttachAddressRequest> validator,
            [FromServices] ParcelExistsValidator parcelExistsValidator,
            [FromServices] IIfMatchHeaderValidator ifMatchHeaderValidator,
            [FromRoute] string caPaKey,
            [FromBody] AttachAddressRequest request,
            [FromHeader(Name = "If-Match")] string? ifMatchHeaderValue,
            CancellationToken cancellationToken = default)
        {
            var vbrCaPaKey = new VbrCaPaKey(caPaKey);
            var parcelId = ParcelId.CreateFor(vbrCaPaKey);

            await validator.ValidateAndThrowAsync(request, cancellationToken);

            try
            {
                if (!await parcelExistsValidator.Exists(parcelId, cancellationToken))
                {
                    throw new ApiException(ValidationErrors.Common.ParcelNotFound.Message, StatusCodes.Status404NotFound);
                }

                if (!await ifMatchHeaderValidator.IsValid(ifMatchHeaderValue, parcelId, cancellationToken))
                {
                    return new PreconditionFailedResult();
                }

                var sqsRequest = new AttachAddressSqsRequest
                {
                    Request = request,
                    VbrCaPaKey = vbrCaPaKey,
                    IfMatchHeaderValue = ifMatchHeaderValue,
                    Metadata = GetMetadata(),
                    ProvenanceData = new ProvenanceData(CreateProvenance(Modification.Update))
                };

                var sqsResult = await _mediator.Send(sqsRequest, cancellationToken);

                return Accepted(sqsResult);
            }
            catch (AggregateNotFoundException)
            {
                throw new ApiException(ValidationErrors.Common.ParcelNotFound.Message, StatusCodes.Status404NotFound);
            }
        }
    }
}
