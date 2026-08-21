package com.example.e_imza_app_backend_new.service;

import com.example.e_imza_app_backend_new.dto.DocumentDto;
import com.example.e_imza_app_backend_new.model.Document;
import com.example.e_imza_app_backend_new.model.User;
import com.example.e_imza_app_backend_new.repository.DocumentRepository;
import com.example.e_imza_app_backend_new.repository.UserRepository;
import io.minio.BucketExistsArgs;
import io.minio.GetObjectArgs;
import io.minio.MakeBucketArgs;
import io.minio.MinioClient;
import io.minio.PutObjectArgs;
import jakarta.annotation.PostConstruct;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.core.io.ByteArrayResource;
import org.springframework.core.io.Resource;
import org.springframework.http.*;
import org.springframework.security.core.userdetails.UsernameNotFoundException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.util.LinkedMultiValueMap;
import org.springframework.util.MultiValueMap;
import org.springframework.web.client.RestTemplate;
import org.springframework.web.multipart.MultipartFile;

import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
@Slf4j // Loglama için eklendi
public class DocumentService {

    private static final long MAX_PDF_SIZE = 50L * 1024 * 1024;
    private static final long MAX_USER_STORAGE = 1024L * 1024 * 1024;

    private final DocumentRepository documentRepository;
    private final UserRepository userRepository;
    private final MinioClient minioClient;
    private final RestTemplate restTemplate;

    @Value("${minio.bucket-name}")
    private String bucketName;

    @Value("${signing-service.csharp-url}")
    private String csharpSigningServiceUrl;

    @Value("${signing-service.api-key}")
    private String signingServiceApiKey;

    // YENİ METOT: Uygulama başladığında MinIO bucket'ını kontrol eder/oluşturur.
    @PostConstruct
    public void init() {
        try {
            boolean found = minioClient.bucketExists(BucketExistsArgs.builder().bucket(bucketName).build());
            if (!found) {
                minioClient.makeBucket(MakeBucketArgs.builder().bucket(bucketName).build());
                log.info("MinIO bucket '{}' oluşturuldu.", bucketName);
            } else {
                log.info("MinIO bucket '{}' zaten mevcut.", bucketName);
            }
        } catch (Exception e) {
            throw new RuntimeException("MinIO bucket oluşturulurken hata oluştu!", e);
        }
    }

    @Transactional
    public Document storeFile(MultipartFile file, String username) {
        String originalFileName = validatePdf(file);
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new UsernameNotFoundException("Kullanıcı bulunamadı: " + username));
        ensureStorageQuota(user, file.getSize(), 0L);

        try {
            // Her kullanıcının nesneleri MinIO içinde ayrı bir önek altında tutulur.
            // Orijinal dosya adı yalnızca veritabanında kalır; nesne anahtarına kullanıcı girdisi yazılmaz.
            String storedFileName = "users/" + user.getId() + "/" + UUID.randomUUID() + ".pdf";

            // Dosyayı MinIO'ya yükle
            minioClient.putObject(
                    PutObjectArgs.builder()
                            .bucket(bucketName)
                            .object(storedFileName)
                            .stream(file.getInputStream(), file.getSize(), -1)
                            .contentType(file.getContentType())
                            .build()
            );

            Document document = Document.builder()
                    .user(user)
                    .originalFileName(originalFileName)
                    .storedFileName(storedFileName)
                    .fileType(MediaType.APPLICATION_PDF_VALUE)
                    .fileSize(file.getSize())
                    .status("YUKLENDI")
                    .build();

            return documentRepository.save(document);

        } catch (Exception e) {
            throw new RuntimeException("Dosya yüklenirken bir hata oluştu: " + e.getMessage(), e);
        }
    }

    // ... (getDocumentsForUser, downloadFile, uploadSignedDocument ve signDocumentWithCSharpService metotları aynı kalacak) ...
    // ... Sadece kopyala-yapıştır kolaylığı için aşağıya ekliyorum ...

    @Transactional(readOnly = true)
    public List<DocumentDto> getDocumentsForUser(String username) {
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new UsernameNotFoundException("Kullanıcı bulunamadı: " + username));
        List<Document> documents = documentRepository.findByUser_Id(user.getId());
        return documents.stream()
                .map(doc -> DocumentDto.builder()
                        .id(doc.getId())
                        .originalFileName(doc.getOriginalFileName())
                        .status(doc.getStatus())
                        .uploadedAt(doc.getUploadedAt())
                        .build())
                .collect(Collectors.toList());
    }

    public Map<String, Object> downloadFile(Long id, String username) {
        Document document = documentRepository.findByIdAndUser_Username(id, username)
                .orElseThrow(DocumentNotFoundException::new);
        try (InputStream stream = minioClient.getObject(
                GetObjectArgs.builder()
                        .bucket(bucketName)
                        .object(document.getStoredFileName())
                        .build())) {
            byte[] content = stream.readAllBytes();
            Resource resource = new ByteArrayResource(content);
            Map<String, Object> result = new HashMap<>();
            result.put("resource", resource);
            result.put("contentType", document.getFileType());
            result.put("filename", document.getOriginalFileName());
            return result;
        } catch (Exception e) {
            throw new RuntimeException("Dosya indirilirken hata oluştu: " + e.getMessage(), e);
        }
    }

    @Transactional
    public void uploadSignedDocument(Long id, MultipartFile signedFile, String username) {
        validatePdf(signedFile);
        Document document = documentRepository.findByIdAndUser_Username(id, username)
                .orElseThrow(DocumentNotFoundException::new);
        long previousSize = document.getFileSize() == null ? 0L : document.getFileSize();
        ensureStorageQuota(document.getUser(), signedFile.getSize(), previousSize);
        try {
            minioClient.putObject(
                    PutObjectArgs.builder()
                            .bucket(bucketName)
                            .object(document.getStoredFileName())
                            .stream(signedFile.getInputStream(), signedFile.getSize(), -1)
                            .contentType(signedFile.getContentType())
                            .build()
            );
            document.setStatus("İMZALANDI");
            document.setFileSize(signedFile.getSize());
            documentRepository.save(document);
        } catch (Exception e) {
            throw new RuntimeException("İmzalı dosya yüklenirken bir hata oluştu: " + e.getMessage(), e);
        }
    }

    public byte[] signDocumentWithCSharpService(MultipartFile file, String pin, String signaturePosition) {
        String originalFileName = validatePdf(file);
        if (pin == null || !pin.matches("^[0-9]{4,16}$")) {
            throw new IllegalArgumentException("PIN 4-16 rakamdan oluşmalıdır.");
        }
        if (!java.util.Set.of("BottomRight", "BottomLeft", "TopRight", "TopLeft").contains(signaturePosition)) {
            throw new IllegalArgumentException("Geçersiz imza konumu.");
        }
        try {
            HttpHeaders headers = new HttpHeaders();
            headers.setContentType(MediaType.MULTIPART_FORM_DATA);
            headers.set("X-Signer-Key", signingServiceApiKey);
            MultiValueMap<String, Object> body = new LinkedMultiValueMap<>();
            ByteArrayResource fileAsResource = new ByteArrayResource(file.getBytes()) {
                @Override
                public String getFilename() {
                    return originalFileName;
                }
            };
            body.add("file", fileAsResource);
            body.add("pin", pin);
            body.add("signaturePosition", signaturePosition);
            HttpEntity<MultiValueMap<String, Object>> requestEntity = new HttpEntity<>(body, headers);
            ResponseEntity<byte[]> response = restTemplate.postForEntity(
                    csharpSigningServiceUrl,
                    requestEntity,
                    byte[].class
            );
            if (response.getStatusCode().is2xxSuccessful() && response.getBody() != null) {
                return response.getBody();
            } else {
                throw new RuntimeException("C# İmzalama servisi bir hata döndürdü. Status: " + response.getStatusCode());
            }
        } catch (Exception e) {
            throw new RuntimeException("C# servisine bağlanırken hata oluştu: " + e.getMessage(), e);
        }
    }

    private String validatePdf(MultipartFile file) {
        if (file == null || file.isEmpty()) {
            throw new IllegalArgumentException("Boş dosya yüklenemez.");
        }
        if (file.getSize() > MAX_PDF_SIZE) {
            throw new IllegalArgumentException("PDF belgesi en fazla 50 MB olabilir.");
        }

        String suppliedName = file.getOriginalFilename() == null ? "belge.pdf" : file.getOriginalFilename();
        String safeName = suppliedName.replace('\\', '/');
        safeName = safeName.substring(safeName.lastIndexOf('/') + 1)
                .replace("\r", "")
                .replace("\n", "")
                .replace("\0", "")
                .trim();
        if (safeName.isBlank() || safeName.length() > 200 || !safeName.toLowerCase(java.util.Locale.ROOT).endsWith(".pdf")) {
            throw new IllegalArgumentException("Geçerli ve en fazla 200 karakterlik bir PDF dosya adı kullanın.");
        }

        try (InputStream stream = file.getInputStream()) {
            byte[] prefix = stream.readNBytes(1024);
            String header = new String(prefix, StandardCharsets.ISO_8859_1);
            if (!header.contains("%PDF-")) {
                throw new IllegalArgumentException("Dosya içeriği geçerli bir PDF belgesi değil.");
            }
        } catch (IllegalArgumentException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new IllegalArgumentException("PDF belgesi okunamadı.");
        }

        return safeName;
    }

    private void ensureStorageQuota(User user, long incomingSize, long replacedSize) {
        long currentUsage = documentRepository.totalFileSizeByUserId(user.getId());
        if (currentUsage - replacedSize + incomingSize > MAX_USER_STORAGE) {
            throw new IllegalArgumentException("Kullanıcı başına 1 GB belge kotası aşılıyor.");
        }
    }
}
