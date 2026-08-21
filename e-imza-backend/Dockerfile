FROM maven:3.9-eclipse-temurin-17-alpine AS build

WORKDIR /app
COPY pom.xml .
COPY src ./src

RUN --mount=type=cache,target=/root/.m2 \
    mvn --batch-mode clean package -DskipTests

FROM eclipse-temurin:17-jre-alpine

WORKDIR /app
RUN addgroup -S eimza && adduser -S -G eimza -h /app eimza
COPY --from=build --chown=eimza:eimza /app/target/*.jar app.jar

USER eimza

EXPOSE 5050

ENTRYPOINT ["java", "-jar", "app.jar"]
