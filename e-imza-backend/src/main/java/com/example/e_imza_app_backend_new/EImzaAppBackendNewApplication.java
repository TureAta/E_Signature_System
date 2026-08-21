package com.example.e_imza_app_backend_new;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.context.annotation.Bean;
import org.springframework.web.client.RestTemplate;


@SpringBootApplication
public class EImzaAppBackendNewApplication {

    public static void main(String[] args) {
        SpringApplication.run(EImzaAppBackendNewApplication.class, args);
    }


    @Bean
    public RestTemplate restTemplate() {
        return new RestTemplate();
    }
}