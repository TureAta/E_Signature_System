package com.example.e_imza_app_backend_new.dto;

import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@AllArgsConstructor
@NoArgsConstructor
public class RegisterRequest {
    @NotBlank(message = "E-posta adresi zorunludur.")
    @Email(message = "Geçerli bir e-posta adresi girin.")
    @Size(max = 254, message = "E-posta adresi çok uzun.")
    private String email;

    @NotBlank(message = "Kullanıcı adı zorunludur.")
    @Size(min = 3, max = 50, message = "Kullanıcı adı 3-50 karakter arasında olmalıdır.")
    @Pattern(regexp = "^[\\p{L}\\p{N}._-]+$", message = "Kullanıcı adı yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.")
    private String username;

    @NotBlank(message = "Şifre zorunludur.")
    @Size(min = 10, max = 72, message = "Şifre 10-72 karakter arasında olmalıdır.")
    private String password;
}
