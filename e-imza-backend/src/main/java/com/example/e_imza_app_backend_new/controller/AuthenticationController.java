package com.example.e_imza_app_backend_new.controller;

import com.example.e_imza_app_backend_new.dto.AuthenticationResponse;
import com.example.e_imza_app_backend_new.dto.LoginRequest;
import com.example.e_imza_app_backend_new.dto.RegisterRequest;
import com.example.e_imza_app_backend_new.service.AuthenticationService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.HttpHeaders;
import org.springframework.http.ResponseCookie;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.Duration;
import java.util.Map;

@RestController
@RequestMapping("/api/auth") // Tüm endpoint'ler bu yolla başlayacak
@RequiredArgsConstructor
public class AuthenticationController {

    private final AuthenticationService authenticationService;

    @Value("${security.cookie-secure:false}")
    private boolean secureCookie;

    @Value("${jwt.expiration}")
    private long expirationMs;

    // KAYIT ENDPOINT'İ
    @PostMapping("/register")
    public ResponseEntity<Map<String, String>> register(
            @Valid @RequestBody RegisterRequest request
    ) {
        AuthenticationResponse authentication = authenticationService.register(request);
        return authenticatedResponse(authentication.getToken(), request.getUsername());
    }

    // GİRİŞ ENDPOINT'İ
    @PostMapping("/login")
    public ResponseEntity<Map<String, String>> login(
            @Valid @RequestBody LoginRequest request
    ) {
        AuthenticationResponse authentication = authenticationService.login(request);
        return authenticatedResponse(authentication.getToken(), request.getUsername());
    }

    @PostMapping("/logout")
    public ResponseEntity<Void> logout() {
        ResponseCookie cookie = ResponseCookie.from("eimza_session", "")
                .httpOnly(true)
                .secure(secureCookie)
                .sameSite("Strict")
                .path("/api")
                .maxAge(Duration.ZERO)
                .build();
        return ResponseEntity.noContent().header(HttpHeaders.SET_COOKIE, cookie.toString()).build();
    }

    @GetMapping("/health")
    public ResponseEntity<Map<String, String>> health() {
        return ResponseEntity.ok(Map.of("status", "ok"));
    }

    private ResponseEntity<Map<String, String>> authenticatedResponse(String token, String username) {
        ResponseCookie cookie = ResponseCookie.from("eimza_session", token)
                .httpOnly(true)
                .secure(secureCookie)
                .sameSite("Strict")
                .path("/api")
                .maxAge(Duration.ofMillis(expirationMs))
                .build();
        return ResponseEntity.ok()
                .header(HttpHeaders.SET_COOKIE, cookie.toString())
                .body(Map.of("username", username.trim().toLowerCase(java.util.Locale.ROOT)));
    }
}
