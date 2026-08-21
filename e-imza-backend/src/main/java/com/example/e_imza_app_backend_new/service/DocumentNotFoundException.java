package com.example.e_imza_app_backend_new.service;

public class DocumentNotFoundException extends RuntimeException {
    public DocumentNotFoundException() {
        super("Doküman bulunamadı.");
    }
}
