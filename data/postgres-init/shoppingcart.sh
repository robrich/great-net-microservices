#!/bin/sh
set -e

echo "Creating shoppingcart database..."

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "postgres" <<-EOSQL

  CREATE USER shoppingcart
  WITH PASSWORD 'shoppingcart';

  CREATE DATABASE shoppingcart;

  GRANT all privileges ON DATABASE shoppingcart TO shoppingcart;

EOSQL

# FRAGILE: ASSUME: the products inserted in the other db were ids 1-5
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "shoppingcart" <<-EOSQL

  CREATE TABLE products (
    id int PRIMARY KEY,
    price DECIMAL(10, 2) NOT NULL
  );

  INSERT INTO products (id, price) VALUES
    (1, 9.99),
    (2, 8.75),
    (3, 5.00),
    (4, 2.49),
    (5, 0.99);

  GRANT select, insert, update ON TABLE products TO shoppingcart;

  CREATE TABLE users (
    id int PRIMARY KEY,
    zipcode varchar(10) NOT NULL
  );

  INSERT INTO users (id, zipcode) VALUES
    (1, 12345),
    (2, 23456),
    (3, 98765);

  GRANT select, insert, update ON TABLE users TO shoppingcart;

  CREATE TABLE orders (
    id SERIAL PRIMARY KEY,
    productid int NOT NULL,
    userid int NOT NULL,
    quantity DECIMAL(10, 2) NOT NULL,
    price DECIMAL(10, 2) NOT NULL,
    subtotal DECIMAL(10, 2) NOT NULL,
    tax DECIMAL(10, 2) NOT NULL,
    total DECIMAL(10, 2) NOT NULL,
    FOREIGN KEY (productid) REFERENCES products (id),
    FOREIGN KEY (userid) REFERENCES users (id)
  );

  GRANT select, insert, update ON TABLE orders TO shoppingcart;

  GRANT usage, select ON ALL SEQUENCES IN SCHEMA public to shoppingcart;

EOSQL
