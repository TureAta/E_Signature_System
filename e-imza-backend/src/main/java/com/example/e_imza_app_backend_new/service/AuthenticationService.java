package com.example.e_imza_app_backend_new.service;

import com.example.e_imza_app_backend_new.dto.AuthenticationResponse;
import com.example.e_imza_app_backend_new.dto.LoginRequest;
import com.example.e_imza_app_backend_new.dto.RegisterRequest;
import com.example.e_imza_app_backend_new.model.User;
import com.example.e_imza_app_backend_new.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import jakarta.annotation.PostConstruct;
import org.springframework.security.authentication.BadCredentialsException;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;

import java.util.Locale;

@Service
@RequiredArgsConstructor
public class AuthenticationService {
    private final UserRepository userRepository;
    private final JwtService jwtService;
    private final PasswordEncoder passwordEncoder;
    private final LoginAttemptLimiter loginAttemptLimiter;
    private String dummyPasswordHash;

    @PostConstruct
    void initializeDummyHash() {
        dummyPasswordHash = passwordEncoder.encode("not-a-real-password");
    }

    // YENİDEN DÜZENLENMİŞ REGISTER METODU
    public AuthenticationResponse register(RegisterRequest request) {
        String username = request.getUsername().trim().toLowerCase(Locale.ROOT);
        String email = request.getEmail().trim().toLowerCase(Locale.ROOT);
        // Kullanıcı adı veya email daha önce alınmış mı diye kontrol et
        if (userRepository.findByUsername(username).isPresent()) {
            throw new IllegalStateException("Bu kullanıcı adı veya e-posta adresi kullanılamıyor.");
        }
        if (userRepository.findByEmail(email).isPresent()) {
            throw new IllegalStateException("Bu kullanıcı adı veya e-posta adresi kullanılamıyor.");
        }

        // Yeni bir kullanıcı nesnesi oluştur
        var user = User.builder()
                .username(username)
                .email(email)
                // Şifreyi veritabanına kaydetmeden önce mutlaka şifrele (hash'le)
                .password(passwordEncoder.encode(request.getPassword()))
                .build();

        // Kullanıcıyı veritabanına kaydet
        userRepository.save(user);

        // Yeni kullanıcı için bir JWT token oluştur ve döndür
        var jwtToken = jwtService.generateToken(user);
        return new AuthenticationResponse(jwtToken);
    }

    // MEVCUT LOGIN METODU (Değişiklik yok)
    public AuthenticationResponse login(LoginRequest request) {
        String username = request.getUsername().trim().toLowerCase(Locale.ROOT);
        loginAttemptLimiter.assertAllowed(username);
        User user = userRepository.findByUsername(username).orElse(null);
        String passwordHash = user == null ? dummyPasswordHash : user.getPassword();
        if (!passwordEncoder.matches(request.getPassword(), passwordHash) || user == null) {
            loginAttemptLimiter.recordFailure(username);
            throw new BadCredentialsException("Geçersiz kullanıcı adı veya şifre");
        }

        loginAttemptLimiter.clear(username);

        String jwtToken = jwtService.generateToken(user);
        return new AuthenticationResponse(jwtToken);
    }
}
