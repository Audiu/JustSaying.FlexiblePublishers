#!/bin/sh

docker-compose -f docker-compose.yml up --build --abort-on-container-exit --exit-code-from testrunner
