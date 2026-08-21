package com.example.e_imza_app_backend_new.config;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import java.io.IOException;
import java.net.URI;
import java.util.Set;

@Component
public class SameOriginWriteFilter extends OncePerRequestFilter {
    private static final Set<String> SAFE_METHODS = Set.of("GET", "HEAD", "OPTIONS");

    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain chain)
            throws ServletException, IOException {
        if (!SAFE_METHODS.contains(request.getMethod())) {
            String fetchSite = request.getHeader("Sec-Fetch-Site");
            if ("cross-site".equalsIgnoreCase(fetchSite) || !originMatchesRequest(request)) {
                response.sendError(HttpServletResponse.SC_FORBIDDEN);
                return;
            }
        }
        chain.doFilter(request, response);
    }

    private boolean originMatchesRequest(HttpServletRequest request) {
        String origin = request.getHeader("Origin");
        if (origin == null || origin.isBlank() || "null".equals(origin)) return true;
        try {
            URI uri = URI.create(origin);
            String expectedHost = request.getHeader("X-Forwarded-Host");
            if (expectedHost == null || expectedHost.isBlank()) expectedHost = request.getHeader("Host");
            String expectedScheme = request.getHeader("X-Forwarded-Proto");
            if (expectedScheme == null || expectedScheme.isBlank()) expectedScheme = request.getScheme();
            String originHost = uri.getHost() + (uri.getPort() == -1 ? "" : ":" + uri.getPort());
            return expectedScheme.equalsIgnoreCase(uri.getScheme()) && expectedHost.equalsIgnoreCase(originHost);
        } catch (RuntimeException exception) {
            return false;
        }
    }
}
