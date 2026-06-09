using TechnoSac.FullTank.Platform.Iam.Application.CommandServices;
using TechnoSac.FullTank.Platform.Iam.Application.Internal.OutboundServices;
using TechnoSac.FullTank.Platform.Iam.Domain.Model;
using TechnoSac.FullTank.Platform.Iam.Domain.Model.Aggregates;
using TechnoSac.FullTank.Platform.Iam.Domain.Model.Commands;
using TechnoSac.FullTank.Platform.Iam.Domain.Repositories;
using TechnoSac.FullTank.Platform.Resources.Errors;
using TechnoSac.FullTank.Platform.Shared.Application.Model;
using TechnoSac.FullTank.Platform.Shared.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace TechnoSac.FullTank.Platform.Iam.Application.Internal.CommandServices;

/// <summary>Handles user commands: sign-in, sign-up, profile update and password change.</summary>
public class UserCommandService(
    IUserRepository userRepository,
    ITokenService tokenService,
    IHashingService hashingService,
    IUnitOfWork unitOfWork,
    IStringLocalizer<ErrorMessages> localizer)
    : IUserCommandService
{
    public async Task<Result<(User user, string token)>> Handle(SignInCommand command,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByEmailAsync(command.Email, cancellationToken);

        if (user == null || !hashingService.VerifyPassword(command.Password, user.PasswordHash))
            return Result<(User user, string token)>.Failure(IamError.InvalidCredentials,
                localizer[nameof(IamError.InvalidCredentials)]);

        var token = tokenService.GenerateToken(user);

        return Result<(User user, string token)>.Success((user, token));
    }

    public async Task<Result<User>> Handle(SignUpCommand command, CancellationToken cancellationToken)
    {
        if (await userRepository.ExistsByEmailAsync(command.Email, cancellationToken))
            return Result<User>.Failure(IamError.EmailAlreadyTaken,
                localizer[nameof(IamError.EmailAlreadyTaken), command.Email]);

        var username = string.IsNullOrWhiteSpace(command.Username) ? command.Email : command.Username;
        if (await userRepository.ExistsByUsernameAsync(username, cancellationToken))
            return Result<User>.Failure(IamError.UsernameAlreadyTaken,
                localizer[nameof(IamError.UsernameAlreadyTaken), username]);

        var hashedPassword = hashingService.HashPassword(command.Password);
        var user = new User(command, hashedPassword);
        try
        {
            await userRepository.AddAsync(user, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<User>.Success(user);
        }
        catch (OperationCanceledException)
        {
            return Result<User>.Failure(IamError.OperationCancelled, localizer[nameof(IamError.OperationCancelled)]);
        }
        catch (DbUpdateException)
        {
            return Result<User>.Failure(IamError.DatabaseError, localizer[nameof(IamError.DatabaseError)]);
        }
        catch (Exception)
        {
            return Result<User>.Failure(IamError.InternalServerError, localizer[nameof(IamError.InternalServerError)]);
        }
    }

    public async Task<Result<User>> Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
            return Result<User>.Failure(IamError.UserNotFound, localizer[nameof(IamError.UserNotFound)]);

        try
        {
            user.UpdateProfile(command.Name, command.Email);
            userRepository.Update(user);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<User>.Success(user);
        }
        catch (DbUpdateException)
        {
            return Result<User>.Failure(IamError.DatabaseError, localizer[nameof(IamError.DatabaseError)]);
        }
        catch (Exception)
        {
            return Result<User>.Failure(IamError.InternalServerError, localizer[nameof(IamError.InternalServerError)]);
        }
    }

    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(IamError.UserNotFound, localizer[nameof(IamError.UserNotFound)]);

        if (!hashingService.VerifyPassword(command.CurrentPassword, user.PasswordHash))
            return Result.Failure(IamError.InvalidCredentials, localizer[nameof(IamError.InvalidCredentials)]);

        try
        {
            user.ChangePasswordHash(hashingService.HashPassword(command.NewPassword));
            userRepository.Update(user);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception)
        {
            return Result.Failure(IamError.PasswordChangeFailed, localizer[nameof(IamError.PasswordChangeFailed)]);
        }
    }
}
