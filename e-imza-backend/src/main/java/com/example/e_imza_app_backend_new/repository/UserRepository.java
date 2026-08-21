package com.example.e_imza_app_backend_new.repository;

import com.example.e_imza_app_backend_new.model.User;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;
import java.util.Optional;
import java.util.UUID;

@Repository
// UserRepository.java içine eklenecek
public interface UserRepository extends JpaRepository<User, UUID> { // ID tipi de UUID olarak güncellenmeli
    Optional<User> findByUsername(String username);
    Optional<User> findByEmail(String email); // Bu satırı ekle
}