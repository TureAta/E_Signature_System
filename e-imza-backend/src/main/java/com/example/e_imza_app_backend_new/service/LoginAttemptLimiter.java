package com.example.e_imza_app_backend_new.service;

import org.springframework.stereotype.Service;

import java.time.Duration;
import java.time.Instant;
import java.util.concurrent.ConcurrentHashMap;

@Service
public class LoginAttemptLimiter {
    private static final int MAX_FAILURES = 10;
    private static final Duration WINDOW = Duration.ofMinutes(5);
    private final ConcurrentHashMap<String, AttemptWindow> attempts = new ConcurrentHashMap<>();

    public void assertAllowed(String username) {
        AttemptWindow window = attempts.get(username);
        if (window != null && !window.expired() && window.count() >= MAX_FAILURES) {
            throw new TooManyAuthAttemptsException();
        }
        if (window != null && window.expired()) attempts.remove(username, window);
    }

    public void recordFailure(String username) {
        attempts.compute(username, (key, current) ->
                current == null || current.expired()
                        ? new AttemptWindow(1, Instant.now().plus(WINDOW))
                        : new AttemptWindow(current.count() + 1, current.expiresAt()));
    }

    public void clear(String username) {
        attempts.remove(username);
    }

    private record AttemptWindow(int count, Instant expiresAt) {
        private boolean expired() {
            return Instant.now().isAfter(expiresAt);
        }
    }
}
