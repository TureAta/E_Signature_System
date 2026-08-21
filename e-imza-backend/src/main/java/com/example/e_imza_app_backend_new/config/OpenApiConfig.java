package com.example.e_imza_app_backend_new.config;

import io.swagger.v3.oas.models.Components;
import io.swagger.v3.oas.models.OpenAPI;
import io.swagger.v3.oas.models.info.Info;
import io.swagger.v3.oas.models.security.SecurityRequirement;
import io.swagger.v3.oas.models.security.SecurityScheme;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class OpenApiConfig {

    /**
     * Swagger/OpenAPI dokümantasyonu için temel yapılandırmayı oluşturur.
     */
    @Bean
    public OpenAPI customOpenAPI() {
        // Bu, Swagger UI'da görünecek olan temel API bilgileridir.
        Info apiInfo = new Info()
                .title("E-İmza Projesi API")
                .version("1.0.0")
                .description("Bu API, E-İmza projesinin backend servislerini belgeler.");

        // Bu bölüm, JWT Bearer Token ile kimlik doğrulamanın nasıl yapılacağını tanımlar.
        // Swagger UI'da sağ üstte bir "Authorize" butonu ekler.
        SecurityScheme securityScheme = new SecurityScheme()
                .name("bearerAuth") // Bu şemaya verdiğimiz isim
                .type(SecurityScheme.Type.HTTP)
                .scheme("bearer")
                .bearerFormat("JWT")
                .in(SecurityScheme.In.HEADER)
                .name("Authorization");

        // Bu, tüm endpoint'lerin varsayılan olarak bu güvenlik şemasını gerektirdiğini belirtir.
        SecurityRequirement securityRequirement = new SecurityRequirement().addList("bearerAuth");

        return new OpenAPI()
                .info(apiInfo)
                .components(new Components().addSecuritySchemes("bearerAuth", securityScheme))
                .addSecurityItem(securityRequirement);
    }
}