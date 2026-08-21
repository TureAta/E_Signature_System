package com.example.e_imza_app_backend_new.repository;

import com.example.e_imza_app_backend_new.model.Document;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;
import java.util.List;
import java.util.UUID;
import java.util.Optional;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

@Repository
// JpaRepository<Document, Long> kısmı DOĞRU, çünkü Document'ın kendi ID'si Long tipinde.
public interface DocumentRepository extends JpaRepository<Document, Long> {


    List<Document> findByUser_Id(UUID userId);
    Optional<Document> findByIdAndUser_Username(Long id, String username);

    @Query("select coalesce(sum(d.fileSize), 0) from Document d where d.user.id = :userId")
    long totalFileSizeByUserId(@Param("userId") UUID userId);
}
