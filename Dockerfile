FROM node:24 AS build
RUN mkdir /build
WORKDIR /build
COPY package.json tsconfig.json jest.config.ts /build/
RUN npm install
COPY src /build/src/
RUN ls -lt
RUN npm run build


FROM node:24
ARG VERSION
RUN mkdir /app
WORKDIR /app
ENTRYPOINT [ "/usr/local/bin/node" ]
CMD [ "build/index.js" ]
COPY --from=build /build/node_modules /app/node_modules
RUN mkdir -p static/res
COPY robots.txt *.html *.css *.js /app/static/
COPY lib /app/static/lib/
COPY res /app/static/res/
COPY make /app/static/make/
COPY print /app/static/print/
COPY t5ss /app/static/t5ss/
COPY package.json /app/package.json
ENV PACKAGE_VERSION=${VERSION:-}

COPY --from=build /build/build /app/build/



