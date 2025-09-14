using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IUserService, UserService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Global error handling middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var errorResponse = new { error = "Internal server error." };
        await context.Response.WriteAsJsonAsync(errorResponse);
        Console.WriteLine($"[{DateTime.Now}] ERROR: {ex.Message}");
    }
});

// Logging middleware
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var path = context.Request.Path;

    await next();

    var statusCode = context.Response.StatusCode;
    Console.WriteLine($"[{DateTime.Now}] {method} {path} => {statusCode}");
});


// Token validation middleware
app.Use(async (context, next) =>
{
    // Example: Expect token in Authorization header as "Bearer {token}"
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (authHeader is null || !authHeader.StartsWith("Bearer "))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized: Missing or invalid token." });
        return;
    }

    var token = authHeader.Substring("Bearer ".Length).Trim();

    if (!IsValidToken(token))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized: Invalid token." });
        return;
    }

    await next();
});

// Token validation helper
static bool IsValidToken(string token)
{
    // Example: Accept only "secrettoken" as valid
    return token == "secrettoken";
}


// CRUD endpoints for User Management

app.MapGet("/users", ([FromServices] IUserService userService) =>
{
    // Return a copy to avoid exposing internal list
    return Results.Ok(userService.GetAll().ToList());
});

app.MapGet("/users/{id}", ([FromServices] IUserService userService, int id) =>
{
    var user = userService.GetById(id);
    if (user is null)
        return Results.NotFound(new { message = $"User with ID {id} not found." });
    return Results.Ok(user);
});

app.MapPost("/users", ([FromServices] IUserService userService, [FromBody] User user) =>
{
    var validation = ValidateUser(user);
    if (!validation.IsValid)
        return Results.BadRequest(new { message = validation.ErrorMessage });

    userService.Add(user);
    return Results.Created($"/users/{user.Id}", user);
});

app.MapPut("/users/{id}", ([FromServices] IUserService userService, int id, [FromBody] User updatedUser) =>
{
    var validation = ValidateUser(updatedUser);
    if (!validation.IsValid)
        return Results.BadRequest(new { message = validation.ErrorMessage });

    var result = userService.Update(id, updatedUser);
    if (!result)
        return Results.NotFound(new { message = $"User with ID {id} not found." });
    return Results.NoContent();
});

app.MapDelete("/users/{id}", ([FromServices] IUserService userService, int id) =>
{
    var result = userService.Delete(id);
    if (!result)
        return Results.NotFound(new { message = $"User with ID {id} not found." });
    return Results.NoContent();
});

app.Run();

// User model
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// User service interface
public interface IUserService
{
    IEnumerable<User> GetAll();
    User? GetById(int id);
    void Add(User user);
    bool Update(int id, User user);
    bool Delete(int id);
}

// Simple in-memory user service
public class UserService : IUserService
{
    private readonly List<User> _users = new();
    private int _nextId = 1;

    public IEnumerable<User> GetAll() => _users.ToList();

    public User? GetById(int id) => _users.FirstOrDefault(u => u.Id == id);

    public void Add(User user)
    {
        user.Id = _nextId++;
        _users.Add(user);
    }

    public bool Update(int id, User user)
    {
        var existing = GetById(id);
        if (existing is null) return false;
        existing.Name = user.Name;
        existing.Email = user.Email;
        return true;
    }

    public bool Delete(int id)
    {
        var user = GetById(id);
        if (user is null) return false;
        _users.Remove(user);
        return true;
    }


    // Validation helper
    static (bool IsValid, string ErrorMessage) ValidateUser(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Name))
            return (false, "Name cannot be empty.");
        if (string.IsNullOrWhiteSpace(user.Email) || !Regex.IsMatch(user.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return (false, "Invalid email address.");
        return (true, string.Empty);
    }
}