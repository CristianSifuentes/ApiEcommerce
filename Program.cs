using System.Text;
using ApiEcommerce.Constants;
using ApiEcommerce.Models;
using ApiEcommerce.Repository;
using ApiEcommerce.Repository.IRepository;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var dbConnectionString = builder.Configuration.GetConnectionString("ConexionSql");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(dbConnectionString));

builder.Services.AddResponseCaching(options =>
{
  options.MaximumBodySize = 1024 * 1024;
  options.UseCaseSensitivePaths = true;
});

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
// Esta linea agrega el servicio de AutoMapper
// AutoMapper busca en el ensamblado actual (donde se encuentra la clase Program) los perfiles de mapeo
// y los registra automaticamente
// De esta forma, no es necesario registrar cada perfil de mapeo manualmente
// Solo debemos crear las clases que hereden de Profile y definir los mapeos
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// En esta parte configuramos Identity
// Identity usa por defecto la tabla AspNetUsers para los usuarios
// y AspNetRoles para los roles
// Si queremos usar una tabla diferente, debemos crear una clase que herede de IdentityUser
// y otra que herede de IdentityRole
// y luego configurar Identity para que use esas clases
// En este caso, ApplicationUser hereda de IdentityUser
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configuramos la autenticacion con JWT
// Leemos la clave secreta desde appsettings.json
// y la usamos para validar los tokens
var secretKey = builder.Configuration.GetValue<string>("ApiSettings:SecretKey");
if (string.IsNullOrEmpty(secretKey))
{
  throw new InvalidOperationException("SecretKey no esta configurada");
}
// Configuramos el esquema de autenticacion
// Usamos JWT Bearer
// Configuramos los parametros de validacion del token
// como la clave secreta, el emisor y el publico
// En este caso, no validamos el emisor ni el publico
builder.Services.AddAuthentication(options =>
{
  // Configuramos el esquema por defecto de autenticacion
  // y el esquema por defecto de desafio
  // Ambos son JWT Bearer
  // ¿Que es esquema por defecto?
  // Esquema que se usa cuando no se especifica uno
  // en el atributo [Authorize] o en la llamada a HttpContext.AuthenticateAsync()
  // ¿Que es esquema por defecto de desafio?
  // Esquema que se usa cuando se requiere autenticacion
  // y no se proporciona un token valido
  options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
  options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
  options.RequireHttpsMetadata = false;
  options.SaveToken = true;
  options.TokenValidationParameters = new TokenValidationParameters
  {
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
    ValidateIssuer = false,
    ValidateAudience = false
  };
})
;

builder.Services.AddControllers(option =>
{
  option.CacheProfiles.Add(CacheProfiles.Default10, CacheProfiles.Profile10);
  option.CacheProfiles.Add(CacheProfiles.Default20, CacheProfiles.Profile20);
}
);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(
  options =>
  {
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
      Description = "Nuestra API utiliza la Autenticación JWT usando el esquema Bearer. \n\r\n\r" +
                    "Ingresa la palabra a continuación el token generado en login.\n\r\n\r" +
                    "Ejemplo: \"12345abcdef\"",
      Name = "Authorization",
      In = ParameterLocation.Header,
      Type = SecuritySchemeType.Http,
      Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
      {
        new OpenApiSecurityScheme
        {
          Reference = new OpenApiReference
          {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
          },
          Scheme = "oauth2",
          Name = "Bearer",
          In = ParameterLocation.Header
        },
        new List<string>()
      }
    });
    options.SwaggerDoc("v1", new OpenApiInfo
    {
      Version = "v1",
      Title = "API Ecommerce",
      Description = "API para gestionar productos y usuarios",
      TermsOfService = new Uri("http://example.com/terms"),
      Contact = new OpenApiContact
      {
        Name = "DevTalles",
        Url = new Uri("https://devtalles.com")
      },
      License = new OpenApiLicense
      {
        Name = "Licencia de uso",
        Url = new Uri("http://example.com/license")
      }
    });
    options.SwaggerDoc("v2", new OpenApiInfo
    {
      Version = "v2",
      Title = "API Ecommerce V2",
      Description = "API para gestionar productos y usuarios",
      TermsOfService = new Uri("http://example.com/terms"),
      Contact = new OpenApiContact
      {
        Name = "DevTalles",
        Url = new Uri("https://devtalles.com")
      },
      License = new OpenApiLicense
      {
        Name = "Licencia de uso",
        Url = new Uri("http://example.com/license")
      }
    });
  }
);

var apiVersioningBuilder = builder.Services.AddApiVersioning(option =>
{
  //AssumeDefaultVersionWhenUnspecified = true: Si el cliente no especifica una versión, se usará la versión por defecto.
  // DefaultApiVersion = new ApiVersion(1, 0): Establece la versión por defecto de la API a la 1.0.
  // ReportApiVersions = true: Incluye en las respuestas de la API los encabezados
  option.AssumeDefaultVersionWhenUnspecified = true;
  option.DefaultApiVersion = new ApiVersion(1, 0);
  option.ReportApiVersions = true;
  // option.ApiVersionReader = ApiVersionReader.Combine(new QueryStringApiVersionReader("api-version")); //?api-version
});

apiVersioningBuilder.AddApiExplorer(option =>
{
  //GroupNameFormat: Define el formato del nombre de la versión en la URL.
  //SubstituteApiVersionInUrl: Sustituye la versión en la URL de los
  option.GroupNameFormat = "'v'VVV"; // v1,v2,v3...
  option.SubstituteApiVersionInUrl = true; // api/v{version}/products
});
builder.Services.AddCors(options =>
  {
    // Define una política de CORS llamada "AllowSpecificOrigin" que permite solicitudes desde cualquier origen.
    // Esto es útil para permitir el acceso a la API desde diferentes dominios durante el desarrollo o en producción.
    // La política permite cualquier método HTTP y cualquier encabezado en las solicitudes.
    // Puedes ajustar los orígenes permitidos según tus necesidades de seguridad.
    options.AddPolicy(PolicyNames.AllowSpecificOrigin,
    builder =>
    {
      builder.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
    }
    );
  }
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI(options =>
  {
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.SwaggerEndpoint("/swagger/v2/swagger.json", "v2");
  });
}

app.UseHttpsRedirection();

app.UseCors(PolicyNames.AllowSpecificOrigin);

app.UseResponseCaching();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
