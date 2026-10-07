package com.tvgun.tv.core;

import java.util.Locale;

/**
 * tvgun 线协议工具：UDP "x,y" 瞄准包解析/格式化、POST /shot JSON 解析与响应构造、
 * 自动发现报文常量。JSON 为手写最小解析，只处理协议内结构（扁平对象 + 数字值）。
 */
public final class Protocol {

    /** 手机广播的发现请求载荷。 */
    public static final String DISCOVERY_REQUEST = "TVGUN_DISCOVER";

    /** 发现应答前缀，后随空格与服务端口。 */
    public static final String DISCOVERY_REPLY_PREFIX = "TVGUN_HERE";

    private Protocol() {
    }

    /**
     * 解析 UDP 瞄准包 "x,y"（1 位小数 ASCII）。
     * @return {x, y}；畸形输入返回 null（接收方静默丢弃）。
     */
    public static double[] parseAim(String text) {
        if (text == null) {
            return null;
        }
        String s = text.trim();
        int comma = s.indexOf(',');
        if (comma <= 0 || comma != s.lastIndexOf(',') || comma == s.length() - 1) {
            return null;
        }
        try {
            double x = Double.parseDouble(s.substring(0, comma).trim());
            double y = Double.parseDouble(s.substring(comma + 1).trim());
            if (Double.isNaN(x) || Double.isNaN(y)
                    || Double.isInfinite(x) || Double.isInfinite(y)) {
                return null;
            }
            return new double[]{x, y};
        } catch (NumberFormatException e) {
            return null;
        }
    }

    /** 格式化瞄准包，与手机端一致：1 位小数、逗号无空格。 */
    public static String formatAim(double x, double y) {
        return String.format(Locale.US, "%.1f,%.1f", x, y);
    }

    /**
     * 解析 /shot 请求体 {"x":123.4,"y":567.8}。
     * @return {x, y}；结构不符返回 null。
     */
    public static double[] parseShotJson(String body) {
        if (body == null) {
            return null;
        }
        Double x = jsonNumber(body, "x");
        Double y = jsonNumber(body, "y");
        if (x == null || y == null) {
            return null;
        }
        return new double[]{x, y};
    }

    /**
     * 在扁平 JSON 对象文本中找 "key" : number。仅支持协议内结构：
     * 键必须带双引号，值为 JSON 数字（允许负号/小数/科学计数法）。
     */
    static Double jsonNumber(String body, String key) {
        String quotedKey = "\"" + key + "\"";
        int i = body.indexOf(quotedKey);
        if (i < 0) {
            return null;
        }
        i += quotedKey.length();
        int n = body.length();
        while (i < n && Character.isWhitespace(body.charAt(i))) {
            i++;
        }
        if (i >= n || body.charAt(i) != ':') {
            return null;
        }
        i++;
        while (i < n && Character.isWhitespace(body.charAt(i))) {
            i++;
        }
        int start = i;
        while (i < n) {
            char ch = body.charAt(i);
            if ((ch >= '0' && ch <= '9') || ch == '-' || ch == '+'
                    || ch == '.' || ch == 'e' || ch == 'E') {
                i++;
            } else {
                break;
            }
        }
        if (i == start) {
            return null;
        }
        try {
            return Double.parseDouble(body.substring(start, i));
        } catch (NumberFormatException e) {
            return null;
        }
    }

    /** /shot 固定响应（手机端只用于震动/闪屏反馈，不校验语义）。 */
    public static String shotResponse() {
        return "{\"hit\":true,\"score\":0}";
    }

    /** /coin /start /reload 扩展端点响应。 */
    public static String okResponse() {
        return "{\"ok\":true}";
    }

    /** GET /health 响应；aimAgeMs < 0 表示槽空。 */
    public static String healthResponse(long aimAgeMs) {
        return aimAgeMs >= 0
                ? "{\"ok\":true,\"aimAge\":" + aimAgeMs + "}"
                : "{\"ok\":true,\"aimAge\":null}";
    }

    /** 400 错误响应。 */
    public static String errorResponse(String message) {
        return "{\"error\":\"" + message + "\"}";
    }
}
