import logger from "./logger.js";


export function requestLogger(req: Request, res: Response, next: () => void) {
    const start = Date.now();

    logger.info(`[***] ${req.method} ${req.url} ...`);


    next();

    const end = Date.now();
    const duration = end-start;

    logger.info(`[${(res as any)?.statusCode}] ${req.method} ${req.url} ${duration}ms`);
}
