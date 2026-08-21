package com.example.e_imza_app_backend_new.dto;

import lombok.Builder;
import lombok.Data;

import java.time.LocalDateTime;

@Data
@Builder
public class DocumentDto {
    private Long id;
    private String originalFileName;
    private String status;
    private LocalDateTime uploadedAt;
}