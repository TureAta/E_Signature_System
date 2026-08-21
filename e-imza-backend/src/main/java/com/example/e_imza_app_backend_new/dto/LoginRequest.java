package com.example.e_imza_app_backend_new.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;
import lombok.Data;

@Data
public class LoginRequest {
    @NotBlank(message = "Kullanıcı adı zorunludur.")
    @Size(max = 50, message = "Kullanıcı adı en fazla 50 karakter olabilir.")
    private String username;

    @NotBlank(message = "Şifre zorunludur.")
    @Size(max = 72, message = "Şifre en fazla 72 karakter olabilir.")
    private String password;
}
