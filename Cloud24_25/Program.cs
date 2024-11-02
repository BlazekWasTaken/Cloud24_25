var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/helloworld", () => "Hello World!")
    .WithName("HelloWorld")
    .WithOpenApi();

app.MapPost("/handle-file", async (IFormFile myFile) =>
    {
        // do something with file
    })
    .WithName("HandleFile")
    .DisableAntiforgery();

app.Run();