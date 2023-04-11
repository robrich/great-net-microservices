#!/bin/sh
set -e

echo "Creating productcatalog database..."

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "postgres" <<-EOSQL

  CREATE USER productcatalog
  WITH PASSWORD 'productcatalog';

  CREATE DATABASE productcatalog;

  GRANT all privileges ON DATABASE productcatalog TO productcatalog;

EOSQL

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "productcatalog" <<-EOSQL

  CREATE TABLE products (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    price DECIMAL(10, 2) NOT NULL,
    description TEXT
  );

  INSERT INTO products (name, price, description) VALUES
    ('ReactJS', 9.99, 'A popular JavaScript library for building user interfaces'),
    ('Angular', 8.75, 'A comprehensive JavaScript framework for building web applications'),
    ('Vue.js', 5.00, 'A progressive JavaScript framework for building user interfaces'),
    ('Ember.js', 2.49, 'A framework for ambitious web developers'),
    ('Backbone.js', 0.99, 'A lightweight JavaScript framework for building client-side applications');

  GRANT select, insert, update ON TABLE products TO productcatalog;

  GRANT usage, select ON ALL SEQUENCES IN SCHEMA public to productcatalog;

EOSQL
