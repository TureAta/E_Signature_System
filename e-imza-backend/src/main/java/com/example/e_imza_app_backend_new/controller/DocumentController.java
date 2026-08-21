package com.example.e_imza_app_backend_new.controller;

import com.example.e_imza_app_backend_new.dto.DocumentDto;
import com.example.e_imza_app_backend_new.service.DocumentService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import lombok.RequiredArgsConstructor;
import org.springframework.core.io.Resource;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.multipart.MultipartFile;

import java.security.Principal;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;

@RestController
@RequestMapping("/api/documents")
@RequiredArgsConstructor
public class DocumentController {

    private final DocumentService documentService;

    @PostMapping("/upload")
    public ResponseEntity<String> uploadDocument(
            @RequestParam("file") MultipartFile file,
            Principal principal
    ) {
        documentService.storeFile(file, principal.getName());
        return ResponseEntity.ok("Dosya '" + file.getOriginalFilename() + "' başarıyla yüklendi.");
    }

    @GetMapping("/my-documents")
    public ResponseEntity<List<DocumentDto>> listMyDocuments(Principal principal) {
        List<DocumentDto> documents = documentService.getDocumentsForUser(principal.getName());
        return ResponseEntity.ok(documents);
    }

    @GetMapping("/{id}/download")
    public ResponseEntity<Resource> downloadDocument(@PathVariable Long id, Principal principal) {
        Map<String, Object> documentData = documentService.downloadFile(id, principal.getName());
        Resource resource = (Resource) documentData.get("resource");
        String contentType = (String) documentData.get("contentType");
        String filename = (String) documentData.get("filename");

        return ResponseEntity.ok()
                .contentType(MediaType.parseMediaType(contentType))
                .header(HttpHeaders.CONTENT_DISPOSITION, org.springframework.http.ContentDisposition.attachment()
                        .filename(filename, StandardCharsets.UTF_8)
                        .build().toString())
                .header("X-Content-Type-Options", "nosniff")
                .body(resource);
    }

    @PostMapping("/{id}/upload-signed")
    public ResponseEntity<String> uploadSignedDocument(
            @PathVariable Long id,
            @RequestParam("file") MultipartFile signedFile,
            Principal principal
    ) {
        documentService.uploadSignedDocument(id, signedFile, principal.getName());
        return ResponseEntity.ok("İmzalı doküman başarıyla yüklendi.");
    }

    @PostMapping(value = "/sign-with-service", consumes = MediaType.MULTIPART_FORM_DATA_VALUE)
    @Operation(
            summary = "Belgeyi C# Servisi ile İmzala",
            description = "Bir PDF belgesini, PIN kodunu ve imza pozisyonunu alır, arkadaki C# mikroservisine göndererek imzalar ve imzalı belgeyi geri döner.",
            responses = {
                    @ApiResponse(responseCode = "200", description = "Belge başarıyla imzalandı",
                            content = @Content(mediaType = MediaType.APPLICATION_PDF_VALUE)),
                    @ApiResponse(responseCode = "400", description = "Geçersiz istek (dosya veya PIN eksik)"),
                    @ApiResponse(responseCode = "500", description = "İmzalama sırasında sunucuda bir hata oluştu")
            }
    )
    public ResponseEntity<byte[]> signDocumentWithService(
            @Parameter(description = "İmzalanacak PDF dosyası.", required = true)
            @RequestParam("file") MultipartFile file,

            @Parameter(description = "E-imza token PIN kodu.", required = true, example = "12345")
            @RequestParam("pin") String pin,

            // YENİ EKLENEN PARAMETRE
            @Parameter(description = "İmzanın atılacağı konum. Olası değerler: BottomRight, BottomLeft, TopRight, TopLeft", required = true, example = "BottomRight")
            @RequestParam("signaturePosition") String signaturePosition
    ) {
        // Servisi yeni parametre ile çağırıyoruz
        byte[] signedDocument = documentService.signDocumentWithCSharpService(file, pin, signaturePosition);

        return ResponseEntity.ok()
                .header(HttpHeaders.CONTENT_DISPOSITION, "attachment; filename=\"signed_" + file.getOriginalFilename() + "\"")
                .contentType(MediaType.APPLICATION_PDF)
                .body(signedDocument);
    }
}
