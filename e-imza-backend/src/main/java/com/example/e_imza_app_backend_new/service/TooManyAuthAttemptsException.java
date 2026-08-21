package com.example.e_imza_app_backend_new.service;

public class TooManyAuthAttemptsException extends RuntimeException {
    public TooManyAuthAttemptsException() {
        super("Çok fazla başarısız giriş denemesi yapıldı.");
    }
}
