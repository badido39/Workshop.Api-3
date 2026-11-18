# 03-postgres · L'API parle à PostgreSQL

L'API utilise maintenant **Entity Framework Core + PostgreSQL**. La table est créée au démarrage (`EnsureCreated`).

La chaîne de connexion est dans `appsettings.json` (`Host=localhost`), surchargeable par la variable d'environnement `ConnectionStrings__Default`.

## 1. En local (API sur votre PC, base dans Docker)

```bash
docker run -d --name workshop-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=workshop -p 5432:5432 postgres:16
dotnet run
```

http://localhost:5000 : les todos sont maintenant dans PostgreSQL.

## 2. Panne n°2 : l'API dans un conteneur avec Host=localhost

```bash
docker build -t workshop-api .
docker run --name workshop-api -p 8080:8080 workshop-api
docker ps          # rien !
docker ps -a       # Exited (1)
docker logs workshop-api
```

Le log explique : dans le conteneur, `localhost` = le conteneur lui-même, pas PostgreSQL. C'est ce que **04-compose** résout.

Nettoyage : `docker rm -f workshop-api workshop-pg`.
