package com.example.e_imza_app_backend_new.model;

import jakarta.persistence.*;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.CreationTimestamp;

import java.time.LocalDateTime;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
@Entity
@Table(name = "documents")
public class Document {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY) // Doküman ID'si basit sıralı numara olabilir.
    private Long id;

    // Bu doküman hangi kullanıcıya ait?
    // @ManyToOne: "Birçok doküman, bir kullanıcıya aittir" ilişkisi.
    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "user_id", nullable = false)
    private User user;

    private String originalFileName; // Kullanıcının yüklediği orijinal dosya adı
    private String storedFileName;   // MinIO'da sakladığımız benzersiz dosya adı
    private String fileType;         // Dosyanın tipi (örn: "application/pdf")
    private Long fileSize;           // Kota denetimi için bayt cinsinden boyut
    private String status;           // Dokümanın durumu (örn: "YÜKLENDİ", "İMZALANDI")

    @CreationTimestamp // Kayıt oluşturulduğunda otomatik olarak zaman damgası ekler.
    private LocalDateTime uploadedAt;
}
