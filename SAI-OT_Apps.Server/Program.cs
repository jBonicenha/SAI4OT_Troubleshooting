using Microsoft.Extensions.FileProviders;

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("https://localhost:4200").AllowAnyHeader().AllowAnyMethod();
                      });
});

builder.Services.AddControllers();
builder.Services.AddScoped<SAI_OT_Apps.Server.Services.NetworkDiagramService>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddScoped<SAI_OT_Apps.Server.Services.CodeTesterService>();
builder.Services.AddScoped<SAI_OT_Apps.Server.Services.CodeAuditorService>();
builder.Services.AddSingleton<SAI_OT_Apps.Server.Services.CodeAuditorServiceUDT>(); //Deve ser singleton por conta da service de deleção
builder.Services.AddScoped<SAI_OT_Apps.Server.Services.TroubleshootingService>();
builder.Services.AddScoped<SAI_OT_Apps.Server.Services.CodeGeneratorService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Cria o diretório TemporaryImages se não existir
var tempImagesPath = Path.Combine(Directory.GetCurrentDirectory(), "TemporaryImages");
if (!Directory.Exists(tempImagesPath))
{
    Directory.CreateDirectory(tempImagesPath);
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(tempImagesPath),
    RequestPath = "/temp-images"
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
