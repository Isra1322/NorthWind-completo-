namespace NorthWind.Sales.WebApi;

internal static class Startup
{
    public static WebApplication CreateWebApplication(this WebApplicationBuilder builder)
    {
        bool useFakeUser = builder.Configuration.GetValue<bool>("UserServices:UseFakeUser");
        if (useFakeUser)
            throw new InvalidOperationException("La autenticacion JWT requiere el servicio de usuario real.");

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGenBearer();
        var jwtSettings = builder.Configuration.GetSection(JwtOptions.SectionKey).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Falta configuracion JWT.");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwtSettings.ValidIssuer,
                ValidateAudience = true, ValidAudience = jwtSettings.ValidAudience,
                ValidateLifetime = true, ValidateIssuerSigningKey = true,
                RequireSignedTokens = true, RequireExpirationTime = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ClockSkew = TimeSpan.Zero,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecurityKey))
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddNorthWindSalesServices(options =>
            builder.Configuration.GetSection(DBOptions.SectionKey).Bind(options),
            smtpOptions => builder.Configuration.GetSection(SmtpOptions.SectionKey).Bind(smtpOptions),
            membershipOptions => builder.Configuration.GetSection(MembershipDBOptions.SectionKey).Bind(membershipOptions),
            jwtOptions => builder.Configuration.GetSection(JwtOptions.SectionKey).Bind(jwtOptions),
            useFakeUser);
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin()));
        return builder.Build();
    }

    public static WebApplication ConfigureWebApplication(this WebApplication app)
    {
        app.UseExceptionHandler(builder => { });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNorthWindSalesEndpoints();
        return app;
    }
}
